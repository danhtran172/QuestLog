-- Tim kiem kho metadata: khop TIEN TO thay vi khop tron tu.
--
-- VAN DE: `websearch_to_tsquery('simple', 'unch')` sinh lexeme `'unch'`, va
-- `to_tsvector` cua "Uncharted 2: Among Thieves" chua `'uncharted'`. Hai lexeme
-- khac nhau -> khong khop. Nguoi dung go "unch" thay 0 ket qua, go du
-- "uncharted" moi thay 5. Voi o tim kiem go-toi-dau-hien-toi-do thi nhu vay la
-- hong: ho khong biet phai go het bao nhieu chu moi co gi hien ra.
--
-- CACH SUA: dung `to_tsquery` va gan `:*` vao MOI tu -> `'unch':*` khop moi
-- lexeme bat dau bang "unch". Index GIN tren tsvector ho tro tim tien to san.
--
-- AN TOAN CHUOI: `to_tsquery` nhan mot NGON NGU (`&`, `|`, `!`, `<->`, ngoac).
-- Dua thang chu nguoi dung go vao la bien o tim kiem thanh cho chay cu phap la,
-- va mot dau ngoac le se lam ca cau bao loi. Vi vay:
--   1. thay MOI ky tu khong phai chu/so bang khoang trang
--   2. boc tung tu bang `quote_literal`
-- Sau hai buoc do khong con ky tu nao mang nghia cu phap.
--
-- `nullif(..., '')` chu khong de chuoi rong: `to_tsquery('simple', '')` in ra
-- NOTICE moi lan goi. Cho NULL chay qua thi `search_vector @@ NULL` la NULL, va
-- dong do bi loai — dung y muon, im lang.
--
-- `g.is_published` VAN PHAI CO. Ham nay chay bang service_role nen bypass RLS;
-- bo dieu kien nay la ban ghi chua xuat ban lot thang ra cho user.
--
-- CANH BAO THU TU: 202608240002 va 202609140006 deu chua mot ban
-- `search_metadata_games` CU (khop tron tu). Chay lai mot trong hai file do se
-- ghi de ham nay va tim kiem tien to bien mat ma khong bao gi. File nay phai
-- chay SAU ca hai.

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
  with q as (
    select to_tsquery('simple', nullif((
      select string_agg(quote_literal(word) || ':*', ' & ')
      from regexp_split_to_table(
             regexp_replace(lower(btrim(coalesce(p_query, ''))), '[^[:alnum:]]+', ' ', 'g'),
             '\s+') as word
      where word <> ''
    ), '')) as tsq
  ),
  matches as (
    select g.*,
      count(*) over () as total_count
    from public.metadata_games g, q
    where g.is_published
      and g.search_vector @@ q.tsq
      and (
        p_cursor_name is null
        or (g.name, g.id) > (p_cursor_name, coalesce(p_cursor_id, 0))
      )
    order by g.name asc, g.id asc
    limit greatest(1, least(coalesce(p_limit, 24), 100))
  )
  select m.id, m.igdb_id, m.name, m.developer, m.publisher, m.summary,
         m.publish_date, m.is_dlc, m.base_metadata_game_id, m.poster_url,
         m.backdrop_url, m.banner_url, m.meta_score,
         m.avg_playtime_hours, m.series_id, m.revision, m.total_count
  from matches m;
$$;

revoke execute on function public.search_metadata_games(text, integer, text, bigint) from public, anon, authenticated;
grant execute on function public.search_metadata_games(text, integer, text, bigint) to service_role;
