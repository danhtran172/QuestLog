-- Bo Steam Web API khoi Admin Integrations.
--
-- Khong luong nao doc credential `steam`: anh Steam (banner / hero / poster) lay
-- qua `IStoreQueryService/SearchSuggestions` va CDN cong khai, khong can key.
-- Giu mot o nhap key khong ai dung chi lam admin tuong phai cau hinh no.
--
-- Bang audit giu nguyen rang buoc cu (`'steam'` van hop le) de lich su cu khong
-- vi pham check constraint.

-- Xoa secret Steam (neu tung luu) khoi Vault, roi xoa cau hinh; secret_refs xoa theo cascade.
delete from vault.secrets
where id in (
  select vault_secret_id from integrations.integration_secret_refs where provider = 'steam'
);
delete from integrations.integration_configs where provider = 'steam';

alter table integrations.integration_configs
  drop constraint if exists integration_configs_provider_check;
alter table integrations.integration_configs
  add constraint integration_configs_provider_check
  check (provider in ('rawg', 'igdb', 'cloudinary', 'gemini'));

-- required_fields tra NULL cho provider la -> replace/record_test bao INTEGRATION_PROVIDER_UNKNOWN.
create or replace function integrations.required_fields(p_provider text)
returns text[]
language sql
immutable
set search_path = pg_catalog, integrations
as $$
  select case p_provider
    when 'rawg' then array['apiKey']::text[]
    when 'igdb' then array['clientId', 'clientSecret']::text[]
    when 'cloudinary' then array['cloudName', 'apiKey', 'apiSecret']::text[]
    when 'gemini' then array['apiKey']::text[]
    else null
  end
$$;

create or replace function integrations.list_safe_summaries()
returns jsonb
language sql
stable
security definer
set search_path = pg_catalog, integrations
as $$
  with providers(provider) as (
    values ('rawg'::text), ('igdb'::text), ('cloudinary'::text), ('gemini'::text)
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
