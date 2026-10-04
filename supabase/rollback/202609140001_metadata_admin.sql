drop function if exists public.admin_merge_metadata_series(bigint, bigint, uuid);
drop function if exists public.admin_delete_metadata_series(bigint, uuid);
drop function if exists public.admin_update_metadata_series(bigint, text, uuid);
drop function if exists public.admin_create_metadata_series(text, uuid);

drop trigger if exists metadata_themes_set_updated_at on public.metadata_themes;
drop trigger if exists metadata_genres_set_updated_at on public.metadata_genres;

alter table public.metadata_themes drop column if exists updated_at;
alter table public.metadata_genres drop column if exists updated_at;

drop table if exists public.metadata_audit_events;
