create extension if not exists citext with schema extensions;

create table if not exists public.profiles (
  user_id uuid primary key references auth.users(id) on delete cascade,
  username extensions.citext not null,
  normalized_username text not null,
  email extensions.citext not null,
  role text not null default 'user',
  status text not null default 'active',
  must_change_password boolean not null default false,
  admin_mfa_required boolean not null default false,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  constraint profiles_username_length check (char_length(username::text) between 3 and 32),
  constraint profiles_username_format check (username::text ~ '^[A-Za-z0-9_.-]+$'),
  constraint profiles_normalized_username_format check (
    normalized_username = lower(btrim(username::text))
  ),
  constraint profiles_role_check check (role in ('admin', 'user')),
  constraint profiles_status_check check (status in ('active', 'locked')),
  constraint profiles_username_key unique (normalized_username),
  constraint profiles_email_key unique (email)
);

create index if not exists profiles_role_status_idx
  on public.profiles (role, status);

create table if not exists public.account_audit_events (
  id bigint generated always as identity primary key,
  actor_user_id uuid references auth.users(id) on delete set null,
  subject_user_id uuid references auth.users(id) on delete set null,
  event_type text not null,
  succeeded boolean not null,
  metadata jsonb not null default '{}'::jsonb,
  created_at timestamptz not null default now(),
  constraint account_audit_events_type_not_blank check (btrim(event_type) <> '')
);

create index if not exists account_audit_events_subject_created_idx
  on public.account_audit_events (subject_user_id, created_at desc);

create index if not exists account_audit_events_actor_created_idx
  on public.account_audit_events (actor_user_id, created_at desc);

create or replace function public.set_updated_at()
returns trigger
language plpgsql
set search_path = ''
as $$
begin
  new.updated_at = now();
  return new;
end;
$$;

drop trigger if exists profiles_set_updated_at on public.profiles;
create trigger profiles_set_updated_at
before update on public.profiles
for each row execute function public.set_updated_at();

create or replace function public.handle_new_auth_user()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
declare
  requested_username text;
begin
  requested_username := btrim(coalesce(new.raw_user_meta_data ->> 'username', ''));

  if requested_username = '' then
    raise exception using
      errcode = 'check_violation',
      message = 'USERNAME_REQUIRED';
  end if;

  insert into public.profiles (
    user_id,
    username,
    normalized_username,
    email
  ) values (
    new.id,
    requested_username,
    lower(requested_username),
    new.email
  );

  return new;
end;
$$;

drop trigger if exists on_auth_user_created on auth.users;
create trigger on_auth_user_created
after insert on auth.users
for each row execute function public.handle_new_auth_user();

create or replace function public.custom_access_token_hook(event jsonb)
returns jsonb
language plpgsql
stable
security definer
set search_path = ''
as $$
declare
  claims jsonb;
  account_role text;
  account_status text;
begin
  select p.role, p.status
    into account_role, account_status
  from public.profiles p
  where p.user_id = (event ->> 'user_id')::uuid;

  claims := event -> 'claims';
  claims := jsonb_set(claims, '{user_role}', to_jsonb(coalesce(account_role, 'user')));
  claims := jsonb_set(claims, '{account_status}', to_jsonb(coalesce(account_status, 'locked')));

  return jsonb_set(event, '{claims}', claims);
end;
$$;

alter table public.profiles enable row level security;
alter table public.account_audit_events enable row level security;

drop policy if exists profiles_read_own on public.profiles;
create policy profiles_read_own
on public.profiles
for select
to authenticated
using ((select auth.uid()) = user_id);

drop policy if exists account_audit_events_read_own on public.account_audit_events;
create policy account_audit_events_read_own
on public.account_audit_events
for select
to authenticated
using ((select auth.uid()) = subject_user_id);

revoke all on public.profiles from anon;
revoke all on public.account_audit_events from anon;
revoke insert, update, delete on public.profiles from authenticated;
revoke insert, update, delete on public.account_audit_events from authenticated;
grant select on public.profiles to authenticated;
grant select on public.account_audit_events to authenticated;

grant usage on schema public to supabase_auth_admin;
grant execute on function public.custom_access_token_hook(jsonb) to supabase_auth_admin;
grant select on public.profiles to supabase_auth_admin;
revoke execute on function public.custom_access_token_hook(jsonb) from public, anon, authenticated;

comment on table public.profiles is
  'QuestLog account profile. Supabase Auth remains the source of truth for identities and sessions.';
comment on column public.profiles.normalized_username is
  'Server-normalized, case-insensitive login key. Never trust a client-provided normalized value.';

