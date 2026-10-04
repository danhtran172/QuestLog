-- Shared, admin-managed credentials for external integrations.
-- Credential plaintext exists only inside Vault functions and the server-only RPC
-- result; neither the public schema nor Data API exposes these records.

create schema if not exists integrations;
revoke all on schema integrations from public, anon, authenticated;
grant usage on schema integrations to service_role;

create table if not exists integrations.integration_configs (
  provider text primary key check (provider in ('rawg', 'igdb', 'steam', 'cloudinary', 'gemini')),
  active_revision bigint not null default 0 check (active_revision >= 0),
  configured_fields text[] not null default '{}',
  last_test_status text not null default 'never_tested'
    check (last_test_status in ('never_tested', 'passed', 'failed')),
  last_tested_at timestamptz,
  last_test_error_code text,
  updated_at timestamptz not null default now(),
  updated_by uuid
);

create table if not exists integrations.integration_secret_refs (
  provider text not null references integrations.integration_configs(provider) on delete cascade,
  field_name text not null,
  vault_secret_id uuid not null,
  primary key (provider, field_name)
);

create table if not exists integrations.integration_audit_events (
  id bigint generated always as identity primary key,
  provider text not null check (provider in ('rawg', 'igdb', 'steam', 'cloudinary', 'gemini')),
  action text not null check (action in ('replace', 'test', 'cloudinary_sync_preview', 'cloudinary_sync_execute')),
  actor_user_id uuid,
  occurred_at timestamptz not null default now(),
  revision bigint,
  outcome text not null check (outcome in ('succeeded', 'failed')),
  error_code text
);

create index if not exists integration_audit_events_provider_occurred_at
  on integrations.integration_audit_events(provider, occurred_at desc);

alter table integrations.integration_configs enable row level security;
alter table integrations.integration_secret_refs enable row level security;
alter table integrations.integration_audit_events enable row level security;
revoke all on all tables in schema integrations from public, anon, authenticated;
grant select, insert, update, delete on all tables in schema integrations to service_role;
grant usage, select on all sequences in schema integrations to service_role;

-- `vault.decrypted_secrets` is intentionally read only by these SECURITY DEFINER
-- functions. Do not grant it to PostgREST roles.
create or replace function integrations.required_fields(p_provider text)
returns text[]
language sql
immutable
set search_path = pg_catalog, integrations
as $$
  select case p_provider
    when 'rawg' then array['apiKey']::text[]
    when 'igdb' then array['clientId', 'clientSecret']::text[]
    when 'steam' then array['apiKey']::text[]
    when 'cloudinary' then array['cloudName', 'apiKey', 'apiSecret']::text[]
    when 'gemini' then array['apiKey']::text[]
    else null
  end
$$;

create or replace function integrations.safe_summary(p_provider text)
returns jsonb
language sql
stable
security definer
set search_path = pg_catalog, integrations
as $$
  select jsonb_build_object(
    'provider', provider,
    'configured', active_revision > 0,
    'requiredFields', integrations.required_fields(provider),
    'configuredFields', configured_fields,
    'revision', active_revision,
    'lastTest', jsonb_build_object(
      'status', last_test_status,
      'testedAt', last_tested_at,
      'errorCode', last_test_error_code),
    'updatedAt', updated_at,
    'updatedBy', updated_by)
  from integrations.integration_configs
  where provider = p_provider
$$;

create or replace function integrations.replace_credentials(
  p_provider text,
  p_values jsonb,
  p_actor uuid
)
returns jsonb
language plpgsql
security definer
set search_path = pg_catalog, integrations, vault
as $$
declare
  expected text[];
  actual text[];
  item record;
  existing_secret uuid;
  next_revision bigint;
begin
  expected := integrations.required_fields(p_provider);
  if expected is null then
    raise exception 'INTEGRATION_PROVIDER_UNKNOWN';
  end if;
  if jsonb_typeof(p_values) <> 'object' then
    raise exception 'INTEGRATION_FIELDS_INVALID';
  end if;

  select coalesce(array_agg(key order by key), '{}') into actual
  from jsonb_object_keys(p_values) key;
  if actual is distinct from (select array_agg(field order by field) from unnest(expected) field) then
    raise exception 'INTEGRATION_FIELDS_REQUIRED';
  end if;

  for item in select key, value from jsonb_each_text(p_values) loop
    if item.value is null or length(btrim(item.value)) = 0 then
      raise exception 'INTEGRATION_VALUE_REQUIRED';
    end if;
  end loop;

  insert into integrations.integration_configs(provider)
  values (p_provider)
  on conflict (provider) do nothing;

  for item in select key, value from jsonb_each_text(p_values) loop
    select vault_secret_id into existing_secret
    from integrations.integration_secret_refs
    where provider = p_provider and field_name = item.key
    for update;

    if existing_secret is null then
      existing_secret := vault.create_secret(
        item.value,
        format('questlog.%s.%s', p_provider, item.key),
        'QuestLog Admin Integration credential');
      insert into integrations.integration_secret_refs(provider, field_name, vault_secret_id)
      values (p_provider, item.key, existing_secret);
    else
      perform vault.update_secret(existing_secret, item.value);
    end if;
  end loop;

  update integrations.integration_configs
  set active_revision = active_revision + 1,
      configured_fields = expected,
      last_test_status = 'never_tested',
      last_tested_at = null,
      last_test_error_code = null,
      updated_at = now(),
      updated_by = p_actor
  where provider = p_provider
  returning active_revision into next_revision;

  insert into integrations.integration_audit_events(provider, action, actor_user_id, revision, outcome)
  values (p_provider, 'replace', p_actor, next_revision, 'succeeded');

  return integrations.safe_summary(p_provider);
exception when others then
  insert into integrations.integration_audit_events(provider, action, actor_user_id, outcome, error_code)
  values (
    coalesce(p_provider, 'rawg'),
    'replace',
    p_actor,
    'failed',
    case when sqlerrm like 'INTEGRATION_%' then sqlerrm else 'INTEGRATION_STORE_FAILED' end);
  raise;
end;
$$;

create or replace function integrations.list_safe_summaries()
returns jsonb
language sql
stable
security definer
set search_path = pg_catalog, integrations
as $$
  with providers(provider) as (
    values ('rawg'::text), ('igdb'::text), ('steam'::text), ('cloudinary'::text), ('gemini'::text)
  )
  select coalesce(jsonb_agg(jsonb_build_object(
    'provider', p.provider,
    'configured', coalesce(c.active_revision, 0) > 0,
    'requiredFields', integrations.required_fields(p.provider),
    'configuredFields', coalesce(c.configured_fields, '{}'::text[]),
    'revision', coalesce(c.active_revision, 0),
    'lastTest', jsonb_build_object(
      'status', coalesce(c.last_test_status, 'never_tested'),
      'testedAt', c.last_tested_at,
      'errorCode', c.last_test_error_code),
    'updatedAt', c.updated_at,
    'updatedBy', c.updated_by) order by p.provider), '[]'::jsonb)
  from providers p
  left join integrations.integration_configs c on c.provider = p.provider
$$;

create or replace function integrations.read_credentials(p_provider text)
returns jsonb
language sql
stable
security definer
set search_path = pg_catalog, integrations, vault
as $$
  select jsonb_build_object(
    'revision', c.active_revision,
    'values', coalesce(jsonb_object_agg(r.field_name, v.decrypted_secret), '{}'::jsonb))
  from integrations.integration_configs c
  join integrations.integration_secret_refs r on r.provider = c.provider
  join vault.decrypted_secrets v on v.id = r.vault_secret_id
  where c.provider = p_provider and c.active_revision > 0
  group by c.active_revision
$$;

create or replace function integrations.record_test(
  p_provider text,
  p_passed boolean,
  p_error_code text,
  p_actor uuid
)
returns jsonb
language plpgsql
security definer
set search_path = pg_catalog, integrations
as $$
declare current_revision bigint;
begin
  if integrations.required_fields(p_provider) is null then
    raise exception 'INTEGRATION_PROVIDER_UNKNOWN';
  end if;

  select active_revision into current_revision
  from integrations.integration_configs where provider = p_provider for update;
  if current_revision is null or current_revision = 0 then
    raise exception 'INTEGRATION_NOT_CONFIGURED';
  end if;

  update integrations.integration_configs
  set last_test_status = case when p_passed then 'passed' else 'failed' end,
      last_tested_at = now(),
      last_test_error_code = case when p_passed then null else nullif(left(p_error_code, 120), '') end
  where provider = p_provider;

  insert into integrations.integration_audit_events(provider, action, actor_user_id, revision, outcome, error_code)
  values (p_provider, 'test', p_actor, current_revision,
    case when p_passed then 'succeeded' else 'failed' end,
    case when p_passed then null else nullif(left(p_error_code, 120), '') end);

  return integrations.safe_summary(p_provider);
end;
$$;

-- Only service_role may invoke these wrappers through PostgREST. They are in the
-- public schema solely because the configured Data API exposes RPCs from it.
create or replace function public.admin_list_integrations()
returns jsonb
language sql
security definer
set search_path = pg_catalog, integrations
as $$ select integrations.list_safe_summaries() $$;

create or replace function public.admin_replace_integration_credentials(
  p_provider text, p_values jsonb, p_actor uuid)
returns jsonb
language sql
security definer
set search_path = pg_catalog, integrations
as $$ select integrations.replace_credentials(p_provider, p_values, p_actor) $$;

create or replace function public.admin_read_integration_credentials(p_provider text)
returns jsonb
language sql
security definer
set search_path = pg_catalog, integrations
as $$ select integrations.read_credentials(p_provider) $$;

create or replace function public.admin_record_integration_test(
  p_provider text, p_passed boolean, p_error_code text, p_actor uuid)
returns jsonb
language sql
security definer
set search_path = pg_catalog, integrations
as $$ select integrations.record_test(p_provider, p_passed, p_error_code, p_actor) $$;

create or replace function public.crawler_igdb_credentials()
returns jsonb
language sql
security definer
set search_path = pg_catalog, integrations
as $$ select integrations.read_credentials('igdb') $$;

revoke all on function public.admin_list_integrations() from public, anon, authenticated;
revoke all on function public.admin_replace_integration_credentials(text, jsonb, uuid) from public, anon, authenticated;
revoke all on function public.admin_read_integration_credentials(text) from public, anon, authenticated;
revoke all on function public.admin_record_integration_test(text, boolean, text, uuid) from public, anon, authenticated;
revoke all on function public.crawler_igdb_credentials() from public, anon, authenticated;
grant execute on function public.admin_list_integrations() to service_role;
grant execute on function public.admin_replace_integration_credentials(text, jsonb, uuid) to service_role;
grant execute on function public.admin_read_integration_credentials(text) to service_role;
grant execute on function public.admin_record_integration_test(text, boolean, text, uuid) to service_role;
grant execute on function public.crawler_igdb_credentials() to service_role;
