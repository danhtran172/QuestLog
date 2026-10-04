create table if not exists public.metadata_series (
  id bigint generated always as identity primary key,
  name text not null,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  constraint metadata_series_name_key unique (name),
  constraint metadata_series_name_not_blank check (btrim(name) <> '')
);

create table if not exists public.metadata_games (
  id bigint generated always as identity primary key,
  igdb_id bigint unique,
  name text not null,
  developer text,
  publisher text,
  summary text,
  publish_date date,
  is_dlc boolean not null default false,
  base_metadata_game_id bigint references public.metadata_games(id) on delete set null,
  poster_url text,
  backdrop_url text,
  banner_url text,
  meta_score integer,
  avg_playtime_hours integer,
  series_id bigint references public.metadata_series(id) on delete set null,
  revision bigint not null default 1,
  -- KHONG dung concat_ws o day: no nhan `VARIADIC "any"` nen Postgres danh dau
  -- STABLE, trong khi generated column bat buoc bieu thuc phai IMMUTABLE
  -- (loi 42P17). coalesce + || tren text thi immutable.
  search_vector tsvector generated always as (
    to_tsvector(
      'simple',
      coalesce(name, '') || ' ' ||
      coalesce(developer, '') || ' ' ||
      coalesce(publisher, '') || ' ' ||
      coalesce(summary, ''))
  ) stored,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  constraint metadata_games_name_not_blank check (btrim(name) <> ''),
  constraint metadata_games_meta_score_check check (meta_score is null or meta_score between 0 and 100),
  constraint metadata_games_avg_playtime_check check (avg_playtime_hours is null or avg_playtime_hours >= 0),
  constraint metadata_games_revision_check check (revision > 0)
);

create table if not exists public.metadata_genres (
  id bigint generated always as identity primary key,
  name text not null unique,
  constraint metadata_genres_name_not_blank check (btrim(name) <> '')
);

create table if not exists public.metadata_themes (
  id bigint generated always as identity primary key,
  name text not null unique,
  constraint metadata_themes_name_not_blank check (btrim(name) <> '')
);

create table if not exists public.metadata_game_genres (
  metadata_game_id bigint not null references public.metadata_games(id) on delete cascade,
  genre_id bigint not null references public.metadata_genres(id) on delete cascade,
  primary key (metadata_game_id, genre_id)
);

create table if not exists public.metadata_game_themes (
  metadata_game_id bigint not null references public.metadata_games(id) on delete cascade,
  theme_id bigint not null references public.metadata_themes(id) on delete cascade,
  primary key (metadata_game_id, theme_id)
);

create index if not exists metadata_games_search_vector_idx
  on public.metadata_games using gin (search_vector);
create index if not exists metadata_games_updated_at_idx
  on public.metadata_games (updated_at desc, id desc);
create index if not exists metadata_games_publish_date_idx
  on public.metadata_games (publish_date, id);
create index if not exists metadata_games_base_idx
  on public.metadata_games (base_metadata_game_id);
create index if not exists metadata_game_genres_genre_idx
  on public.metadata_game_genres (genre_id, metadata_game_id);
create index if not exists metadata_game_themes_theme_idx
  on public.metadata_game_themes (theme_id, metadata_game_id);

drop trigger if exists metadata_series_set_updated_at on public.metadata_series;
create trigger metadata_series_set_updated_at
before update on public.metadata_series
for each row execute function public.set_updated_at();

drop trigger if exists metadata_games_set_updated_at on public.metadata_games;
create trigger metadata_games_set_updated_at
before update on public.metadata_games
for each row execute function public.set_updated_at();

-- `returns table` khoa cung TEN COT dau ra. `alter table ... rename column` khong
-- sua kieu tra ve cua ham, nen ban cu con giu cover_image_url/gallery_image_url/
-- steam_library_image_url va `create or replace` bao 42P13. Phai go han.
drop function if exists public.search_metadata_games(text, integer, text, bigint);

create or replace function public.search_metadata_games(
  p_query text,
  p_limit integer default 24,
  p_cursor_name text default null,
  p_cursor_id bigint default null
)
returns table (
  id bigint,
  igdb_id bigint,
  name text,
  developer text,
  publisher text,
  summary text,
  publish_date date,
  is_dlc boolean,
  base_metadata_game_id bigint,
  poster_url text,
  backdrop_url text,
  banner_url text,
  meta_score integer,
  avg_playtime_hours integer,
  series_id bigint,
  revision bigint,
  total_count bigint
)
language sql
stable
set search_path = public, extensions
as $$
  with matches as (
    select g.*,
      count(*) over () as total_count
    from public.metadata_games g
    where g.search_vector @@ websearch_to_tsquery('simple', btrim(p_query))
      and (
        p_cursor_name is null
        or (g.name, g.id) > (p_cursor_name, coalesce(p_cursor_id, 0))
      )
    order by g.name asc, g.id asc
    limit greatest(1, least(coalesce(p_limit, 24), 100))
  )
  -- Qualify het bang alias `m`: ten cot o `returns table (...)` la OUT parameter,
  -- trung ten voi cot cua CTE. De tran se co nguy co "column reference is ambiguous".
  select m.id, m.igdb_id, m.name, m.developer, m.publisher, m.summary, m.publish_date,
         m.is_dlc, m.base_metadata_game_id, m.poster_url, m.backdrop_url,
         m.banner_url, m.meta_score, m.avg_playtime_hours, m.series_id,
         m.revision, m.total_count
  from matches m;
$$;

alter table public.metadata_series enable row level security;
alter table public.metadata_games enable row level security;
alter table public.metadata_genres enable row level security;
alter table public.metadata_themes enable row level security;
alter table public.metadata_game_genres enable row level security;
alter table public.metadata_game_themes enable row level security;

drop policy if exists metadata_series_read_authenticated on public.metadata_series;
create policy metadata_series_read_authenticated on public.metadata_series
for select to authenticated using (true);
drop policy if exists metadata_games_read_authenticated on public.metadata_games;
create policy metadata_games_read_authenticated on public.metadata_games
for select to authenticated using (true);
drop policy if exists metadata_genres_read_authenticated on public.metadata_genres;
create policy metadata_genres_read_authenticated on public.metadata_genres
for select to authenticated using (true);
drop policy if exists metadata_themes_read_authenticated on public.metadata_themes;
create policy metadata_themes_read_authenticated on public.metadata_themes
for select to authenticated using (true);
drop policy if exists metadata_game_genres_read_authenticated on public.metadata_game_genres;
create policy metadata_game_genres_read_authenticated on public.metadata_game_genres
for select to authenticated using (true);
drop policy if exists metadata_game_themes_read_authenticated on public.metadata_game_themes;
create policy metadata_game_themes_read_authenticated on public.metadata_game_themes
for select to authenticated using (true);

revoke insert, update, delete on public.metadata_series from anon, authenticated;
revoke insert, update, delete on public.metadata_games from anon, authenticated;
revoke insert, update, delete on public.metadata_genres from anon, authenticated;
revoke insert, update, delete on public.metadata_themes from anon, authenticated;
revoke insert, update, delete on public.metadata_game_genres from anon, authenticated;
revoke insert, update, delete on public.metadata_game_themes from anon, authenticated;
revoke execute on function public.search_metadata_games(text, integer, text, bigint) from public, anon, authenticated;
grant execute on function public.search_metadata_games(text, integer, text, bigint) to service_role;
