drop trigger if exists on_auth_user_created on auth.users;
drop function if exists public.handle_new_auth_user();
drop function if exists public.custom_access_token_hook(jsonb);
drop trigger if exists profiles_set_updated_at on public.profiles;
drop function if exists public.set_updated_at();
drop table if exists public.account_audit_events;
drop table if exists public.profiles;

