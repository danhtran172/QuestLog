-- Admin Console — liet ke ban ghi metadata co LOC va SAP XEP.
-- Spec: docs/superpowers/specs/2026-09-14-admin-console-mode-design.md §12.1.
--
-- Phu thuoc 202608240002 (bang metadata_games).
--
-- TAI SAO PHAI LA MOT HAM, khong ghep chuoi filter cua PostgREST o Host nua:
--
-- 1. Sap xep theo cot KHAC `id` lam vo con tro keyset cu. Cursor `id=lt.X` chi
--    dung khi `id` vua la khoa sap xep vua la duy nhat. Sap theo `name` thi phai
--    so sanh CAP (name, id), va cap do khong viet duoc gon trong query string.
--
-- 2. Nam trong sau khoa sap xep co the NULL (publish_date, meta_score,
--    avg_playtime_hours, updated_at, name khong the). NULL lam moi phep so sanh
--    thanh false, nen mot keyset ngay tho se BO SOT toan bo phan NULL — dung loi
--    "nhay ban ghi" ma keyset sinh ra de tranh.
--
-- Quy uoc: NULL luon xuong CUOI o ca hai chieu. "Thieu du lieu" khong bao gio
-- duoc trao len dau chi vi doi chieu sap xep.
--
-- Tie-break luon la `id desc` o ca hai chieu, nen dieu kien keyset cho phan bang
-- nhau chi co mot dang duy nhat.

create or replace function public.admin_list_metadata_games(
  p_query     text    default null,
  p_missing   text    default null,
  p_is_dlc    boolean default null,
  p_series_id bigint  default null,
  p_no_series boolean default null,
  p_year      int     default null,
  p_sort      text    default 'id',
  p_desc      boolean default true,
  -- { "value": <gia tri khoa sap xep cua dong cuoi>, "id": <bigint> }
  p_cursor    jsonb   default null,
  p_limit     int     default 30
)
returns jsonb
language plpgsql
stable
security definer
set search_path = public, extensions
as $$
declare
  v_col     text;
  v_dir     text;
  v_down    boolean := coalesce(p_desc, true);
  v_where   text := 'true';
  v_order   text;
  v_limit   int := least(greatest(coalesce(p_limit, 30), 1), 100);
  v_missing text;
  v_pattern text;
  v_cur_val text;
  v_cur_id  bigint;
  v_rows    jsonb;
  v_last    jsonb;
  v_total   bigint;
  v_next    jsonb := null;
begin
  -- == Khoa sap xep: danh sach DONG ==============================
  -- Ten cot di thang vao SQL dong, nen o day la ranh gioi an toan, khong phai
  -- tien ich. Gia tri la khong khop thi tu choi, tuyet doi khong doan.
  -- `case p_sort when null` KHONG BAO GIO khop (NULL = NULL la unknown), nen
  -- phai coalesce truoc khi so sanh.
  v_col := case coalesce(nullif(btrim(coalesce(p_sort, '')), ''), 'id')
    when 'name'         then 'name'
    when 'publishDate'  then 'publish_date'
    when 'metaScore'    then 'meta_score'
    when 'avgPlaytime'  then 'avg_playtime_hours'
    when 'updatedAt'    then 'updated_at'
    when 'id'           then 'id'
    else null
  end;
  if v_col is null then
    return jsonb_build_object('ok', false, 'code', 'METADATA_SORT_UNKNOWN');
  end if;

  v_dir := case when v_down then 'desc' else 'asc' end;

  -- == Bo loc "thieu" — danh sach DONG ==========================
  -- Ban do nam o DAY chu khong o Host: mot noi duy nhat dinh nghia "thieu" nghia
  -- la gi. Host chi kiem tra khoa co nam trong danh sach hay khong.
  if p_missing is not null and btrim(p_missing) <> '' then
    v_missing := case p_missing
      when 'banner'      then 'banner_url is null'
      when 'poster'       then 'poster_url is null'
      when 'backdrop'        then 'backdrop_url is null'
      when 'developer'      then 'developer is null'
      when 'publisher'      then 'publisher is null'
      when 'publishDate'    then 'publish_date is null'
      when 'metaScore'      then 'meta_score is null'
      when 'avgPlaytime'    then 'avg_playtime_hours is null'
      when 'summary'        then 'summary is null'
      when 'igdbId'         then 'igdb_id is null'
      when 'dlcWithoutBase' then 'is_dlc and base_metadata_game_id is null'
      else null
    end;
    -- Phai kiem tra TRUOC khi noi chuoi: `text || NULL` ra NULL, va mot v_where
    -- NULL se lam ca menh de where bien mat thay vi bao loi.
    if v_missing is null then
      return jsonb_build_object('ok', false, 'code', 'METADATA_FILTER_UNKNOWN');
    end if;
    v_where := v_where || ' and ' || v_missing;
  end if;

  -- == Bo loc con lai ===========================================
  if p_is_dlc is not null then
    v_where := v_where || format(' and is_dlc = %L', p_is_dlc);
  end if;

  -- "Khong thuoc series nao" la mot lua chon RIENG, khong phai series id dac biet.
  -- Dung gia tri quy uoc (-1) o day se bien mot so hop le thanh cu phap ngam.
  if coalesce(p_no_series, false) then
    v_where := v_where || ' and series_id is null';
  elsif p_series_id is not null then
    v_where := v_where || format(' and series_id = %L', p_series_id);
  end if;

  if p_year is not null then
    v_where := v_where || format(
      ' and publish_date >= %L::date and publish_date < %L::date',
      p_year || '-01-01', (p_year + 1) || '-01-01');
  end if;

  if p_query is not null and btrim(p_query) <> '' then
    -- `%` va `_` la wildcard cua LIKE; de nguyen thi o tim kiem tro thanh mot
    -- ngon ngu truy van nho ma nguoi dung khong biet minh dang viet.
    v_pattern := '%' || replace(replace(replace(btrim(p_query), '\', '\\'), '%', '\%'), '_', '\_') || '%';
    v_where := v_where || format(
      ' and (name ilike %L or developer ilike %L or publisher ilike %L)',
      v_pattern, v_pattern, v_pattern);
  end if;

  -- == Tong so ban ghi khop ======================================
  -- Chi dem o TRANG DAU. Dem lai o moi lan "Tai them" la quet ca bang de tra ve
  -- dung con so vua tra, khong doi gi.
  if p_cursor is null then
    execute format('select count(*) from public.metadata_games where %s', v_where)
      into v_total;
  end if;

  -- == Keyset ====================================================
  if p_cursor is not null then
    v_cur_val := p_cursor ->> 'value';
    v_cur_id  := nullif(p_cursor ->> 'id', '')::bigint;

    if v_cur_id is null then
      return jsonb_build_object('ok', false, 'code', 'METADATA_CURSOR_INVALID');
    end if;

    if v_col = 'id' then
      v_where := v_where || format(' and id %s %L', case when v_down then '<' else '>' end, v_cur_id);
    elsif v_cur_val is null then
      -- Da o trong vung NULL (nam cuoi bang): chi con di tiep theo `id`.
      v_where := v_where || format(' and %I is null and id < %L', v_col, v_cur_id);
    else
      -- Con o vung co gia tri. Ba nhanh: vuot qua gia tri cu, bang gia tri cu
      -- nhung id nho hon, hoac da roi xuong vung NULL.
      v_where := v_where || format(
        ' and (%I %s %L or (%I = %L and id < %L) or %I is null)',
        v_col, case when v_down then '<' else '>' end, v_cur_val,
        v_col, v_cur_val, v_cur_id,
        v_col);
    end if;
  end if;

  v_order := case
    when v_col = 'id' then format('id %s', v_dir)
    else format('%I %s nulls last, id desc', v_col, v_dir)
  end;

  -- Lay du `v_limit + 1` dong de biet con trang sau hay khong, ma khong phai dem
  -- lai lan hai.
  execute format(
    'select coalesce(jsonb_agg(to_jsonb(t) order by %s), ''[]''::jsonb)
       from (select id, igdb_id, name, developer, publisher, summary, publish_date,
                    is_dlc, base_metadata_game_id, poster_url, backdrop_url,
                    banner_url, meta_score, avg_playtime_hours, series_id,
                    revision, updated_at
               from public.metadata_games where %s order by %s limit %s) t',
    v_order, v_where, v_order, v_limit + 1)
    into v_rows;

  if jsonb_array_length(v_rows) > v_limit then
    v_rows := (
      select coalesce(jsonb_agg(item), '[]'::jsonb)
      from (
        select item from jsonb_array_elements(v_rows) with ordinality as t(item, n)
        where n <= v_limit
      ) s);
    v_last := v_rows -> (v_limit - 1);
    -- Khoa cua `to_jsonb(row)` chinh la ten cot, nen lay duoc gia tri khoa sap
    -- xep ma khong can truy van them lan nua.
    v_next := jsonb_build_object('value', v_last -> v_col, 'id', v_last -> 'id');
  end if;

  return jsonb_build_object('ok', true, 'data', jsonb_build_object(
    'items', v_rows,
    'nextCursor', v_next,
    'totalItems', v_total));
end;
$$;

revoke all on function public.admin_list_metadata_games(text, text, boolean, bigint, boolean, int, text, boolean, jsonb, int) from public;
revoke all on function public.admin_list_metadata_games(text, text, boolean, bigint, boolean, int, text, boolean, jsonb, int) from anon;
revoke all on function public.admin_list_metadata_games(text, text, boolean, bigint, boolean, int, text, boolean, jsonb, int) from authenticated;
grant execute on function public.admin_list_metadata_games(text, text, boolean, bigint, boolean, int, text, boolean, jsonb, int) to service_role;

-- Index phuc vu sap xep + tie-break. Khong co thi moi lan doi khoa sap xep la
-- mot lan quet toan bang.
create index if not exists metadata_games_name_id_idx on public.metadata_games (name, id desc);
create index if not exists metadata_games_publish_date_id_idx on public.metadata_games (publish_date, id desc);
create index if not exists metadata_games_meta_score_id_idx on public.metadata_games (meta_score, id desc);
create index if not exists metadata_games_avg_playtime_id_idx on public.metadata_games (avg_playtime_hours, id desc);
create index if not exists metadata_games_updated_at_id_idx on public.metadata_games (updated_at, id desc);
create index if not exists metadata_games_series_id_idx on public.metadata_games (series_id);
