-- Admin dashboard — suc khoe kho metadata dung chung.
-- Spec: docs/superpowers/specs/2026-09-14-admin-console-mode-design.md §12.8.
--
-- Phu thuoc 202608240002 (bang metadata) va 202609140001 (audit).
--
-- TOAN BO so lieu tinh o DB trong MOT ham. Cong don o client se sai ngay khi kho
-- vuot mot trang — dung ly do `/api/dashboard/stats` cua thu vien ton tai.
--
-- Ham la `stable`, khong ghi gi, nen khong sinh dong audit.

create or replace function public.admin_metadata_stats()
returns jsonb
language sql
stable
security definer
set search_path = public, extensions
as $$
  with
  games as (select * from public.metadata_games),

  totals as (
    select jsonb_build_object(
      'games',  (select count(*) from games),
      'series', (select count(*) from public.metadata_series),
      'genres', (select count(*) from public.metadata_genres),
      'themes', (select count(*) from public.metadata_themes)
    ) as value
  ),

  by_year as (
    select coalesce(jsonb_agg(jsonb_build_object('label', label, 'value', n) order by label), '[]'::jsonb) as value
    from (
      select extract(year from publish_date)::int::text as label, count(*) as n
      from games where publish_date is not null
      group by 1
    ) t
  ),

  -- Dai diem giu dung nguong 90/80/70/50 cua DOMAIN_RULES §5.5.
  meta_bands as (
    select coalesce(jsonb_agg(jsonb_build_object('label', label, 'value', n) order by sort), '[]'::jsonb) as value
    from (
      select b.label, b.sort, count(g.id) as n
      from (values ('90-100', 1), ('80-89', 2), ('70-79', 3), ('50-69', 4), ('0-49', 5)) as b(label, sort)
      left join games g on g.meta_score is not null and b.label = case
        when g.meta_score >= 90 then '90-100'
        when g.meta_score >= 80 then '80-89'
        when g.meta_score >= 70 then '70-79'
        when g.meta_score >= 50 then '50-69'
        else '0-49' end
      group by b.label, b.sort
    ) t
  ),

  playtime_bands as (
    select coalesce(jsonb_agg(jsonb_build_object('label', label, 'value', n) order by sort), '[]'::jsonb) as value
    from (
      select b.label, b.sort, count(g.id) as n
      from (values ('0-5h', 1), ('6-15h', 2), ('16-30h', 3), ('31-60h', 4), ('60h+', 5)) as b(label, sort)
      left join games g on g.avg_playtime_hours is not null and b.label = case
        when g.avg_playtime_hours <= 5 then '0-5h'
        when g.avg_playtime_hours <= 15 then '6-15h'
        when g.avg_playtime_hours <= 30 then '16-30h'
        when g.avg_playtime_hours <= 60 then '31-60h'
        else '60h+' end
      group by b.label, b.sort
    ) t
  ),

  -- Read-only, phan anh ket qua sync. Rong cho toi khi chot API sync (§4.4).
  top_genres as (
    select coalesce(jsonb_agg(jsonb_build_object('label', name, 'value', n) order by n desc, name), '[]'::jsonb) as value
    from (
      select gr.name, count(*) as n
      from public.metadata_game_genres gg
      join public.metadata_genres gr on gr.id = gg.genre_id
      group by gr.name order by n desc, gr.name limit 12
    ) t
  ),

  top_themes as (
    select coalesce(jsonb_agg(jsonb_build_object('label', name, 'value', n) order by n desc, name), '[]'::jsonb) as value
    from (
      select th.name, count(*) as n
      from public.metadata_game_themes gt
      join public.metadata_themes th on th.id = gt.theme_id
      group by th.name order by n desc, th.name limit 12
    ) t
  ),

  activity as (
    select jsonb_build_object(
      'dlc',        (select count(*) from games where is_dlc),
      'baseGames',  (select count(*) from games where not is_dlc),
      'edited7d',   (select count(*) from games where updated_at >= now() - interval '7 days'),
      'edited30d',  (select count(*) from games where updated_at >= now() - interval '30 days')
    ) as value
  ),

  health as (
    select jsonb_build_object(
      'missingBanner',    (select count(*) from games where banner_url is null),
      'missingPoster',     (select count(*) from games where poster_url is null),
      'missingBackdrop',      (select count(*) from games where backdrop_url is null),
      'missingDeveloper',    (select count(*) from games where developer is null),
      'missingPublisher',    (select count(*) from games where publisher is null),
      'missingPublishDate',  (select count(*) from games where publish_date is null),
      'missingMetaScore',    (select count(*) from games where meta_score is null),
      'missingAvgPlaytime',  (select count(*) from games where avg_playtime_hours is null),
      'missingSummary',      (select count(*) from games where summary is null),
      'missingIgdbId',       (select count(*) from games where igdb_id is null),
      'dlcWithoutBase',      (select count(*) from games where is_dlc and base_metadata_game_id is null),
      -- Khong hanh dong duoc tu console: day la chi so theo doi tien do SYNC (§4.4).
      'withoutTags',         (select count(*) from games g
                              where not exists (select 1 from public.metadata_game_genres x where x.metadata_game_id = g.id)
                                and not exists (select 1 from public.metadata_game_themes y where y.metadata_game_id = g.id)),
      'duplicateNameGroups', (select count(*) from (
                                select lower(btrim(name)) from games group by 1 having count(*) > 1) d),
      'seriesWithOneGame',   (select count(*) from (
                                select s.id from public.metadata_series s
                                join games g on g.series_id = s.id
                                group by s.id having count(g.id) = 1) o),
      'seriesWithNoGames',   (select count(*) from public.metadata_series s
                              where not exists (select 1 from games g where g.series_id = s.id))
    ) as value
  )

  select jsonb_build_object(
    'totals',        (select value from totals),
    'byPublishYear', (select value from by_year),
    'metaScoreBands',(select value from meta_bands),
    'playtimeBands', (select value from playtime_bands),
    'topGenres',     (select value from top_genres),
    'topThemes',     (select value from top_themes),
    'activity',      (select value from activity),
    'health',        (select value from health)
  );
$$;

revoke execute on function public.admin_metadata_stats() from public, anon, authenticated;
grant execute on function public.admin_metadata_stats() to service_role;
