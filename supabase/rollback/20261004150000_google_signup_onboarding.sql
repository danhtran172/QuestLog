-- Rollback for 20261004150000_google_signup_onboarding.sql.
-- Refuses to run while Google accounts without a username exist: restoring
-- NOT NULL would fail, and deleting those accounts must be an explicit decision.

do $$
begin
  if exists (select 1 from public.profiles where username is null) then
    raise exception 'Profiles without a username exist. Delete or complete those accounts before rolling back.';
  end if;
end;
$$;

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

alter table public.profiles drop constraint if exists profiles_complete_has_username;
alter table public.profiles drop constraint if exists profiles_signup_method_check;
alter table public.profiles drop constraint if exists profiles_onboarding_state_check;
alter table public.profiles drop column if exists has_password;
alter table public.profiles drop column if exists signup_method;
alter table public.profiles drop column if exists onboarding_state;
alter table public.profiles alter column normalized_username set not null;
alter table public.profiles alter column username set not null;
