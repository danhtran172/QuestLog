-- Google sign-up: a Google identity creates a profile without a username.
-- The account stays in `username_required` until the Host claims a username.
-- Design: docs/superpowers/specs/2026-10-04-google-signup-email-recovery-design.md §4.

alter table public.profiles alter column username drop not null;
alter table public.profiles alter column normalized_username drop not null;

alter table public.profiles
  add column if not exists onboarding_state text not null default 'complete';
alter table public.profiles
  add column if not exists signup_method text;
alter table public.profiles
  add column if not exists has_password boolean;

-- Existing rows predate Google sign-up: every one of them was created with a
-- password. The only administrator path before this migration was bootstrap.
update public.profiles
   set signup_method = case when role = 'admin' then 'bootstrap' else 'password' end
 where signup_method is null;

update public.profiles
   set has_password = true
 where has_password is null;

alter table public.profiles alter column signup_method set not null;
alter table public.profiles alter column has_password set not null;
alter table public.profiles alter column has_password set default false;

alter table public.profiles drop constraint if exists profiles_onboarding_state_check;
alter table public.profiles
  add constraint profiles_onboarding_state_check
  check (onboarding_state in ('username_required', 'complete'));

alter table public.profiles drop constraint if exists profiles_signup_method_check;
alter table public.profiles
  add constraint profiles_signup_method_check
  check (signup_method in ('password', 'google', 'bootstrap'));

alter table public.profiles drop constraint if exists profiles_complete_has_username;
alter table public.profiles
  add constraint profiles_complete_has_username
  check (onboarding_state <> 'complete' or username is not null);

create or replace function public.handle_new_auth_user()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
declare
  requested_username text;
  auth_provider text;
begin
  auth_provider := coalesce(new.raw_app_meta_data ->> 'provider', 'email');

  -- Google never supplies a QuestLog username. Provider metadata is not a
  -- username choice either, so it is ignored for Google identities.
  if auth_provider = 'google' then
    insert into public.profiles (
      user_id,
      username,
      normalized_username,
      email,
      onboarding_state,
      signup_method,
      has_password
    ) values (
      new.id,
      null,
      null,
      new.email,
      'username_required',
      'google',
      false
    );
    return new;
  end if;

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
    email,
    onboarding_state,
    signup_method,
    has_password
  ) values (
    new.id,
    requested_username,
    lower(requested_username),
    new.email,
    'complete',
    'password',
    true
  );

  return new;
end;
$$;

comment on column public.profiles.onboarding_state is
  'username_required until a Google sign-up claims a username through the Host. Library access is blocked until complete.';
comment on column public.profiles.signup_method is
  'How the account was created. Written by the Auth trigger and the Host only; never client input.';
comment on column public.profiles.has_password is
  'Whether the account has a usable QuestLog password. Maintained by the Host.';
