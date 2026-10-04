-- Go tieu chi xuat ban. Tra `search_metadata_games` va RLS ve trang thai khong loc.

drop trigger if exists metadata_games_apply_gate on public.metadata_games;
drop function if exists public.metadata_games_apply_gate();
drop function if exists public.metadata_recompute_published();
drop function if exists public.admin_set_publish_rules(jsonb, uuid);
drop function if exists public.admin_set_metadata_game_published(bigint, boolean, uuid);
drop function if exists public.admin_get_publish_rules();
drop function if exists public.metadata_gate_failures(text, text, text, text, text, date, integer, integer, text, bigint, boolean, bigint);

drop index if exists public.metadata_games_is_published_idx;

alter table public.metadata_games drop column if exists gate_failures;
alter table public.metadata_games drop column if exists is_published;
alter table public.metadata_games drop column if exists manually_unpublished;

drop table if exists public.metadata_publish_rules;

-- RLS ve lai cho doc tat ca.
drop policy if exists metadata_games_read_authenticated on public.metadata_games;
create policy metadata_games_read_authenticated on public.metadata_games
  for select to authenticated using (true);

-- Ham liet ke ve lai chu ky 10 tham so cua migration 202609140005 — chay lai file
-- do sau khi chay rollback nay.
drop function if exists public.admin_list_metadata_games(text, text, boolean, bigint, boolean, int, text, boolean, jsonb, int, boolean);

-- `search_metadata_games` phai duoc dung lai tu 202608240002 (bo `g.is_published`).
