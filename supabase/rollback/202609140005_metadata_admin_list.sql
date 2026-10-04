drop function if exists public.admin_list_metadata_games(text, text, boolean, bigint, boolean, int, text, boolean, jsonb, int);

drop index if exists public.metadata_games_name_id_idx;
drop index if exists public.metadata_games_publish_date_id_idx;
drop index if exists public.metadata_games_meta_score_id_idx;
drop index if exists public.metadata_games_avg_playtime_id_idx;
drop index if exists public.metadata_games_updated_at_id_idx;
drop index if exists public.metadata_games_series_id_idx;
