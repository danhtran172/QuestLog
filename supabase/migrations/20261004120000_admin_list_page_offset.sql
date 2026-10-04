-- Admin Console: phan trang SO TRANG cho che do danh sach.
--
-- Them `p_offset` vao `admin_list_metadata_games`. Truoc day ham chi co keyset
-- (`p_cursor`) — hop voi nut "Tai them" cua che do the, nhung khong nhay thang
-- toi trang N duoc: muon toi trang 7 phai di qua 6 con tro truoc do.
--
-- HAI CACH PHAN TRANG SONG SONG, khong thay the nhau:
--   p_cursor — che do the ("Tai them"). On dinh khi kho bi sua giua hai lan lat.
--   p_offset — che do danh sach va Preview (nhay trang, "ban ghi i / N"). Kho bi
--              sua giua hai lan lat thi co the lech mot dong; voi man hinh duyet
--              cua admin, doi lay kha nang nhay trang la chap nhan duoc.
-- Co ca hai thi `p_cursor` thang, `p_offset` bi bo qua.
--
-- `totalItems` van chi tinh khi KHONG co con tro, nen moi trang theo offset deu
-- co tong — UI can no de ve "Trang x / y".
--
-- PHAI XOA ban 11 tham so truoc: them tham so la doi chu ky, de lai se thanh
-- overload thu hai va PostgREST co the chon nham.
--
-- Chay SAU 202609140006 (va 202609140007). Chay lai 202609140006 se tao lai ban
-- 11 tham so ben canh ban nay — khi do chay lai file nay.

drop function if exists public.admin_list_metadata_games(text, text, boolean, bigint, boolean, int, text, boolean, jsonb, int, boolean);
drop function if exists public.admin_list_metadata_games(text, text, boolean, bigint, boolean, int, text, boolean, jsonb, int, boolean, int);

create or replace function public.admin_list_metadata_games(
  p_query     text    default null,
  p_missing   text    default null,
  p_is_dlc    boolean default null,
  p_series_id bigint  default null,
  p_no_series boolean default null,
  p_year      int     default null,
  p_sort      text    default 'id',
  p_desc      boolean default true,
  p_cursor    jsonb   default null,
  p_limit     int     default 30,
  -- `true` chi ban ghi DA xuat ban, `false` chi ban ghi chua xuat ban.
  p_published boolean default null,
  -- So ban ghi bo qua o dau. Chi dung khi KHONG co `p_cursor` — xem dau file.
  p_offset    int     default null
)
returns jsonb
language plpgsql
stable
security definer
set search_path = public, extensions
as $fn$
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
  v_offset  int := greatest(coalesce(p_offset, 0), 0);
begin
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
    if v_missing is null then
      return jsonb_build_object('ok', false, 'code', 'METADATA_FILTER_UNKNOWN');
    end if;
    v_where := v_where || ' and ' || v_missing;
  end if;

  -- Loc theo KET QUA (`is_published`), khong theo cot y dinh: admin can biet
  -- "ban ghi nao chua len duoc", khong phan biet vi ai/vi dieu kien nao.
  if p_published is not null then
    v_where := v_where || format(' and is_published = %L', p_published);
  end if;

  if p_is_dlc is not null then
    v_where := v_where || format(' and is_dlc = %L', p_is_dlc);
  end if;

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
    v_pattern := '%' || replace(replace(replace(btrim(p_query), '\', '\\'), '%', '\%'), '_', '\_') || '%';
    v_where := v_where || format(
      ' and (name ilike %L or developer ilike %L or publisher ilike %L)',
      v_pattern, v_pattern, v_pattern);
  end if;

  if p_cursor is null then
    execute format('select count(*) from public.metadata_games where %s', v_where)
      into v_total;
  end if;

  if p_cursor is not null then
    v_cur_val := p_cursor ->> 'value';
    v_cur_id  := nullif(p_cursor ->> 'id', '')::bigint;

    if v_cur_id is null then
      return jsonb_build_object('ok', false, 'code', 'METADATA_CURSOR_INVALID');
    end if;

    if v_col = 'id' then
      v_where := v_where || format(' and id %s %L', case when v_down then '<' else '>' end, v_cur_id);
    elsif v_cur_val is null then
      v_where := v_where || format(' and %I is null and id < %L', v_col, v_cur_id);
    else
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

  -- Con tro da dinh vi trang thi offset khong con nghia gi: cong ca hai se bo
  -- qua ban ghi hai lan.
  if p_cursor is not null then
    v_offset := 0;
  end if;

  execute format(
    'select coalesce(jsonb_agg(to_jsonb(t) - ''search_vector'' order by %s), ''[]''::jsonb)
       from (select * from public.metadata_games where %s order by %s limit %s offset %s) t',
    v_order, v_where, v_order, v_limit + 1, v_offset)
    into v_rows;

  if jsonb_array_length(v_rows) > v_limit then
    v_rows := (
      select coalesce(jsonb_agg(item), '[]'::jsonb)
      from (
        select item from jsonb_array_elements(v_rows) with ordinality as t(item, n)
        where n <= v_limit
      ) s);
    v_last := v_rows -> (v_limit - 1);
    v_next := jsonb_build_object('value', v_last -> v_col, 'id', v_last -> 'id');
  end if;

  return jsonb_build_object('ok', true, 'data', jsonb_build_object(
    'items', v_rows,
    'nextCursor', v_next,
    'totalItems', v_total));
end;
$fn$;

revoke all on function public.admin_list_metadata_games(text, text, boolean, bigint, boolean, int, text, boolean, jsonb, int, boolean, int) from public, anon, authenticated;
grant execute on function public.admin_list_metadata_games(text, text, boolean, bigint, boolean, int, text, boolean, jsonb, int, boolean, int) to service_role;

revoke all on function public.admin_list_metadata_games(text, text, boolean, bigint, boolean, int, text, boolean, jsonb, int, boolean, int) from public, anon, authenticated;
grant execute on function public.admin_list_metadata_games(text, text, boolean, bigint, boolean, int, text, boolean, jsonb, int, boolean, int) to service_role;
