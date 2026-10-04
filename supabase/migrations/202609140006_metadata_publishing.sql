-- Admin Console — TIEU CHI XUAT BAN cua kho metadata.
-- Spec: docs/superpowers/specs/2026-09-14-admin-console-mode-design.md §13.
--
-- Phu thuoc 202608240002 (metadata_games, RLS, search_metadata_games) va
-- 202609140001 (metadata_audit_events).
--
-- HAI KHAI NIEM DOC LAP, khong duoc gop:
--
--   manually_unpublished — Y DINH cua admin. Chi admin ghi.
--   is_published       — KET QUA suy ra. Chi trigger ghi.
--
-- Vi sao khong ghi thang "trot dieu kien" vao `manually_unpublished`: lam vay thi sau
-- khi admin bo sung anh bia, ban ghi VAN an — vi co do do he thong bat chu khong
-- phai admin bat, va khong ai con phan biet duoc nua. Trang thai suy ra ghi de
-- len cot y dinh la mot duong mot chieu.
--
-- `is_published` la cot THAT chu khong phai bieu thuc tinh luc doc:
--   - RLS can mot vi tu re tien tren tung dong; goi ham doc bang cau hinh o moi
--     dong se lam moi truy van cua user cham theo.
--   - Cot that thi index duoc.
-- Doi lai: moi lan doi dieu kien phai quet lai ca bang. Quet mot lan luc doi cau
-- hinh re hon nhieu so voi tinh lai o moi luot doc.

-- == 0) Di doi ten neu ban truoc cua file nay da chay ==========
-- Ban dau tinh nang nay dung tu vung "an / cong khai". Da doi sang "xuat ban /
-- chua xuat ban": mac dinh moi ban ghi DUOC xuat ban, ban ghi truot tieu chi thi
-- tu dong khong len.
--
-- Khoi nay co bao ve nen file chay duoc o CA HAI trang thai — da chay ban cu hay
-- chua chay lan nao. Khong co no thi phai biet chac DB dang o dau moi dam chay.

do $$
begin
  if to_regclass('public.metadata_visibility_rules') is not null
     and to_regclass('public.metadata_publish_rules') is null then
    alter table public.metadata_visibility_rules rename to metadata_publish_rules;
  end if;

  if exists (select 1 from information_schema.columns
             where table_schema = 'public' and table_name = 'metadata_games'
               and column_name = 'manually_hidden') then
    alter table public.metadata_games rename column manually_hidden to manually_unpublished;
  end if;

  if exists (select 1 from information_schema.columns
             where table_schema = 'public' and table_name = 'metadata_games'
               and column_name = 'is_public') then
    alter table public.metadata_games rename column is_public to is_published;
  end if;

  -- `create table if not exists` o duoi se BO QUA bang da ton tai, nen constraint
  -- van giu ten cu. Doi luon cho khoi lech ten voi bang.
  if exists (select 1 from pg_constraint
             where conname = 'metadata_visibility_rules_single_row') then
    alter table public.metadata_publish_rules
      rename constraint metadata_visibility_rules_single_row to metadata_publish_rules_single_row;
  end if;
end
$$;

-- Ham cua ban cu phai bi xoa han: doi ten khong dong nghia voi thay the, va de
-- lai se thanh mot bo API song song khong ai goi nhung van chay duoc.
drop function if exists public.metadata_recompute_visibility();
drop function if exists public.admin_get_visibility_rules();
drop function if exists public.admin_set_visibility_rules(jsonb, uuid);
drop function if exists public.admin_set_metadata_game_hidden(bigint, boolean, uuid);
drop index if exists public.metadata_games_is_public_idx;

-- == 1) Bang tieu chi ==========================================
-- MOT dong duy nhat: `id boolean primary key check (id)` khien dong thu hai
-- khong chen duoc. Dat dieu kien theo COT chu khong theo dong vi vi tu phai
-- goi gon lai thanh mot bieu thuc; moi dieu kien la mot dong thi thanh
-- EXISTS long nhau.

create table if not exists public.metadata_publish_rules (
  id boolean primary key default true,
  require_banner    boolean not null default false,
  require_poster      boolean not null default false,
  require_backdrop       boolean not null default false,
  require_developer     boolean not null default false,
  require_publisher     boolean not null default false,
  require_publish_date  boolean not null default false,
  require_meta_score    boolean not null default false,
  require_avg_playtime  boolean not null default false,
  require_summary       boolean not null default false,
  require_igdb_id       boolean not null default false,
  require_dlc_base      boolean not null default false,
  updated_at timestamptz not null default now(),
  updated_by uuid references auth.users(id) on delete set null,
  constraint metadata_publish_rules_single_row check (id)
);

alter table public.metadata_publish_rules enable row level security;
-- Khong co policy nao: chi service_role (bypass RLS) doc duoc. Cau hinh nay
-- quyet dinh user thay gi, nen ban than no khong phai thu user can doc.
revoke all on public.metadata_publish_rules from anon, authenticated;

-- Mac dinh: KHONG dieu kien nao bat. Bat san mot dieu kien se lam bien mat mot
-- phan kho ngay khi migration chay, ma khong ai chu dong chon dieu do.
insert into public.metadata_publish_rules (id) values (true)
on conflict (id) do nothing;

-- == 2) Cot moi tren metadata_games ============================

alter table public.metadata_games
  add column if not exists manually_unpublished boolean not null default false;

alter table public.metadata_games
  add column if not exists is_published boolean not null default true;

-- Danh sach dieu kien dang truot. Tinh MOT lan trong trigger de giao dien admin
-- noi duoc "an vi thieu anh card" thay vi chi "an".
alter table public.metadata_games
  add column if not exists gate_failures text[] not null default '{}';

create index if not exists metadata_games_is_published_idx
  on public.metadata_games (is_published) where is_published;

-- == 3) Tinh dieu kien truot ===================================
-- Nhan tung field chu khong nhan ca dong: ham nay duoc goi tu trigger BEFORE,
-- noi `new` chua phai mot dong da ton tai.

-- Doi ten tham so = doi chu ky, `create or replace` khong lam duoc.
drop function if exists public.metadata_gate_failures(
  text, text, text, text, text, date, integer, integer, text, bigint, boolean, bigint);

create or replace function public.metadata_gate_failures(
  p_banner       text,
  p_poster       text,
  p_backdrop     text,
  p_developer    text,
  p_publisher    text,
  p_publish_date date,
  p_meta_score   integer,
  p_avg          integer,
  p_summary      text,
  p_igdb         bigint,
  p_is_dlc       boolean,
  p_base         bigint
)
returns text[]
language sql
stable
set search_path = public, extensions
as $$
  select coalesce(
    array_remove(array[
      case when r.require_banner       and p_banner       is null then 'banner'         end,
      case when r.require_poster       and p_poster       is null then 'poster'         end,
      case when r.require_backdrop     and p_backdrop     is null then 'backdrop'       end,
      case when r.require_developer    and p_developer    is null then 'developer'      end,
      case when r.require_publisher    and p_publisher    is null then 'publisher'      end,
      case when r.require_publish_date and p_publish_date is null then 'publishDate'    end,
      case when r.require_meta_score   and p_meta_score   is null then 'metaScore'      end,
      case when r.require_avg_playtime and p_avg          is null then 'avgPlaytime'    end,
      case when r.require_summary      and p_summary      is null then 'summary'        end,
      case when r.require_igdb_id      and p_igdb         is null then 'igdbId'         end,
      case when r.require_dlc_base     and coalesce(p_is_dlc, false) and p_base is null
           then 'dlcWithoutBase' end
    ], null),
    '{}')
  from public.metadata_publish_rules r
  where r.id;
$$;

-- == 4) Trigger giu `gate_failures` va `is_published` dung ========

create or replace function public.metadata_games_apply_gate()
returns trigger
language plpgsql
set search_path = public, extensions
as $$
begin
  new.gate_failures := coalesce(
    public.metadata_gate_failures(
      new.banner_url, new.poster_url, new.backdrop_url,
      new.developer, new.publisher, new.publish_date, new.meta_score,
      new.avg_playtime_hours, new.summary, new.igdb_id,
      new.is_dlc, new.base_metadata_game_id),
    '{}');

  new.is_published := not coalesce(new.manually_unpublished, false)
    and cardinality(new.gate_failures) = 0;

  return new;
end;
$$;

drop trigger if exists metadata_games_apply_gate on public.metadata_games;
create trigger metadata_games_apply_gate
  before insert or update on public.metadata_games
  for each row execute function public.metadata_games_apply_gate();

-- == 5) Quet lai ca bang =======================================
-- Goi sau khi doi dieu kien. KHONG dung `update ... set x = x` de muon trigger:
-- nhu the phu thuoc vao mot hieu ung phu, va bat ky trigger nao them sau nay
-- cung se chay theo.

create or replace function public.metadata_recompute_published()
returns integer
language plpgsql
set search_path = public, extensions
as $$
declare
  v_changed integer;
begin
  with computed as (
    select g.id,
           coalesce(public.metadata_gate_failures(
             g.banner_url, g.poster_url, g.backdrop_url,
             g.developer, g.publisher, g.publish_date, g.meta_score,
             g.avg_playtime_hours, g.summary, g.igdb_id,
             g.is_dlc, g.base_metadata_game_id), '{}') as failures
    from public.metadata_games g
  ),
  updated as (
    update public.metadata_games g
    set gate_failures = c.failures,
        is_published = not g.manually_unpublished and cardinality(c.failures) = 0
    from computed c
    where c.id = g.id
      and (g.gate_failures is distinct from c.failures
           or g.is_published is distinct from (not g.manually_unpublished and cardinality(c.failures) = 0))
    returning 1
  )
  select count(*) into v_changed from updated;

  return v_changed;
end;
$$;

-- == 6) Chi ban ghi CONG KHAI moi den duoc user =================
-- Sua CA HAI duong doc, khong chi mot:
--   - RLS: chan o tang hang, nen mot client goi thang PostgREST cung khong thay.
--   - search_metadata_games: chay bang service_role (bypass RLS) nen phai loc rieng.
-- Thieu mot trong hai thi "kiem soat dau ra" chi la loi khuyen.

drop policy if exists metadata_games_read_authenticated on public.metadata_games;
create policy metadata_games_read_authenticated on public.metadata_games
  for select to authenticated using (is_published);

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
    where g.is_published
      and g.search_vector @@ websearch_to_tsquery('simple', btrim(p_query))
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

-- == 7) Audit: them loai thuc the moi ==========================
-- Dung 'publishing' — chu thuong, mot tu, khop voi 'game' / 'series' / 'genre'
-- / 'theme' da co.
--
-- Ban dau tu vung nay la 'visibility'. Dong audit cu mang gia tri do phai duoc
-- doi sang gia tri moi, neu khong `add constraint` se that bai voi 23514 — va
-- thong bao mac dinh cua Postgres chi noi "bi vi pham boi mot dong nao do",
-- khong noi dong nao.
--
-- THU TU BAT BUOC: GO rang buoc -> DOI du lieu -> SIET lai.
--
-- Doi du lieu truoc khi go rang buoc se bi chinh rang buoc CU chan: no chua biet
-- gia tri 'publishing', va check constraint ap cho ca dong vua UPDATE chu khong
-- rieng dong moi INSERT.
alter table public.metadata_audit_events
  drop constraint if exists metadata_audit_events_entity_check;

update public.metadata_audit_events
set entity = 'publishing'
where entity = 'visibility';

-- Con gia tri la nao khac thi bao RO NO LA GI, thay vi de 23514 noi chung chung.
do $$
declare
  v_bad text;
begin
  select string_agg(distinct entity, ', ')
    into v_bad
  from public.metadata_audit_events
  where entity not in ('game', 'series', 'genre', 'theme', 'publishing');

  if v_bad is not null then
    raise exception
      'metadata_audit_events dang co gia tri entity ngoai danh sach: %. Xu ly cac dong nay truoc khi chay lai migration.',
      v_bad;
  end if;
end
$$;

alter table public.metadata_audit_events
  add constraint metadata_audit_events_entity_check
  check (entity in ('game', 'series', 'genre', 'theme', 'publishing'));

-- == 7b) Hinh dang JSON cua bang tieu chi =======================
-- `to_jsonb(row)` cho ra khoa SNAKE_CASE (ten cot), trong khi ca hop dong API la
-- camelCase. Doi thu cong o day chu KHONG bat client tu doi: doi o client nghia
-- la hop dong co hai hinh dang, va cho nao quen doi thi field im lang thanh
-- `undefined` — dung kieu loi "bat cong tac ma khong luu" vi UI doc lai khong ra.
--
-- Dat thanh HAM de ca duong doc lan duong ghi dung chung mot dinh nghia.

create or replace function public.metadata_publish_rules_json(r public.metadata_publish_rules)
returns jsonb
language sql
immutable
set search_path = public, extensions
as $fn$
  select jsonb_build_object(
    'requireBanner',   r.require_banner,
    'requirePoster',    r.require_poster,
    'requireBackdrop',     r.require_backdrop,
    'requireDeveloper',   r.require_developer,
    'requirePublisher',   r.require_publisher,
    'requirePublishDate', r.require_publish_date,
    'requireMetaScore',   r.require_meta_score,
    'requireAvgPlaytime', r.require_avg_playtime,
    'requireSummary',     r.require_summary,
    'requireIgdbId',      r.require_igdb_id,
    'requireDlcBase',     r.require_dlc_base,
    'updatedAt',          r.updated_at);
$fn$;

revoke all on function public.metadata_publish_rules_json(public.metadata_publish_rules) from public, anon, authenticated;
grant execute on function public.metadata_publish_rules_json(public.metadata_publish_rules) to service_role;

-- == 8) Doi dieu kien ==========================================

create or replace function public.admin_set_publish_rules(
  p_rules jsonb,
  p_actor uuid
)
returns jsonb
language plpgsql
security definer
set search_path = public, extensions
as $$
declare
  v_before public.metadata_publish_rules;
  v_after  public.metadata_publish_rules;
  v_touched integer;
  v_hidden  integer;
begin
  if p_actor is null then
    return jsonb_build_object('ok', false, 'code', 'ACTOR_REQUIRED');
  end if;

  select * into v_before from public.metadata_publish_rules where id;

  -- Field vang mat = GIU NGUYEN, khong phai false. Doi mot cong tac ma vo tinh
  -- tat het cac cong tac khac la kieu mat du lieu khong ai ngo.
  update public.metadata_publish_rules set
    require_banner   = coalesce((p_rules ->> 'requireBanner')::boolean,   require_banner),
    require_poster     = coalesce((p_rules ->> 'requirePoster')::boolean,    require_poster),
    require_backdrop      = coalesce((p_rules ->> 'requireBackdrop')::boolean,     require_backdrop),
    require_developer    = coalesce((p_rules ->> 'requireDeveloper')::boolean,   require_developer),
    require_publisher    = coalesce((p_rules ->> 'requirePublisher')::boolean,   require_publisher),
    require_publish_date = coalesce((p_rules ->> 'requirePublishDate')::boolean, require_publish_date),
    require_meta_score   = coalesce((p_rules ->> 'requireMetaScore')::boolean,   require_meta_score),
    require_avg_playtime = coalesce((p_rules ->> 'requireAvgPlaytime')::boolean, require_avg_playtime),
    require_summary      = coalesce((p_rules ->> 'requireSummary')::boolean,     require_summary),
    require_igdb_id      = coalesce((p_rules ->> 'requireIgdbId')::boolean,      require_igdb_id),
    require_dlc_base     = coalesce((p_rules ->> 'requireDlcBase')::boolean,     require_dlc_base),
    updated_at = now(),
    updated_by = p_actor
  where id
  returning * into v_after;

  v_touched := public.metadata_recompute_published();
  select count(*) into v_hidden from public.metadata_games where not is_published;

  insert into public.metadata_audit_events (actor_user_id, entity, entity_id, action, before, after)
  values (p_actor, 'publishing', null, 'update', to_jsonb(v_before), to_jsonb(v_after));

  return jsonb_build_object('ok', true, 'data', jsonb_build_object(
    'rules', public.metadata_publish_rules_json(v_after),
    'recomputed', v_touched,
    'unpublishedTotal', v_hidden));
end;
$$;

-- == 9) An/hien thu cong mot ban ghi ============================

create or replace function public.admin_set_metadata_game_published(
  p_id bigint,
  -- `true` = admin muon xuat ban, `false` = go xuat ban. Day la Y DINH; ban ghi
  -- truot tieu chi van khong len duoc du y dinh la `true`.
  p_published boolean,
  p_actor uuid
)
returns jsonb
language plpgsql
security definer
set search_path = public, extensions
as $$
declare
  v_before public.metadata_games;
  v_after  public.metadata_games;
begin
  if p_actor is null then
    return jsonb_build_object('ok', false, 'code', 'ACTOR_REQUIRED');
  end if;
  if p_published is null then
    return jsonb_build_object('ok', false, 'code', 'PUBLISHED_REQUIRED');
  end if;

  select * into v_before from public.metadata_games where id = p_id;
  if not found then
    return jsonb_build_object('ok', false, 'code', 'GAME_NOT_FOUND');
  end if;

  -- KHONG dung `revision` o day: an/hien khong doi noi dung ban ghi, nen no khong
  -- duoc lam hong ban dang sua do cua nguoi khac bang mot xung dot 409.
  update public.metadata_games
  set manually_unpublished = not p_published
  where id = p_id
  returning * into v_after;

  insert into public.metadata_audit_events (actor_user_id, entity, entity_id, action, before, after)
  values (p_actor, 'game', p_id, 'update',
          jsonb_build_object('manuallyUnpublished', v_before.manually_unpublished,
                             'isPublished', v_before.is_published),
          jsonb_build_object('manuallyUnpublished', v_after.manually_unpublished,
                             'isPublished', v_after.is_published,
                             'gateFailures', v_after.gate_failures));

  return jsonb_build_object('ok', true, 'data', to_jsonb(v_after) - 'search_vector');
end;
$$;

-- == 10) Quyen ==================================================

revoke all on function public.admin_set_publish_rules(jsonb, uuid) from public, anon, authenticated;
grant execute on function public.admin_set_publish_rules(jsonb, uuid) to service_role;

revoke all on function public.admin_set_metadata_game_published(bigint, boolean, uuid) from public, anon, authenticated;
grant execute on function public.admin_set_metadata_game_published(bigint, boolean, uuid) to service_role;

revoke all on function public.metadata_recompute_published() from public, anon, authenticated;
grant execute on function public.metadata_recompute_published() to service_role;

-- == 11) Dong bo ban ghi da co ==================================
-- Bang da co du lieu tu truoc khi co trigger, nen phai tinh mot lan.
select public.metadata_recompute_published();

-- == 12) Lam moi ham liet ke ====================================
-- THAY THE ban o 202609140005: them ba cot trang thai an/hien va bo loc theo
-- trang thai do. Chay file nay la du, khong phai chay lai migration 7.
--
-- Doi `select <danh sach cot>` thanh `to_jsonb(t) - 'search_vector'`: moi lan
-- them cot vao `metadata_games` ma quen sua danh sach o day la mot cot bi mat
-- lang le. Bo DUNG cot khong muon tra ve thi khong the quen.

-- PHAI XOA CA HAI BAN CU TRUOC KHI TAO LAI:
--
--   10 tham so (migration 7)  — khac chu ky, de lai se thanh mot overload thu hai
--                               va PostgREST co the chon nham.
--   11 tham so voi `p_hidden` (ban dau cua chinh file nay) — CUNG chu ky kieu
--                               nhung KHAC TEN THAM SO. Postgres KHONG cho
--                               `create or replace` doi ten tham so dau vao, nen
--                               khong xoa truoc thi cau lenh duoi se that bai
--                               han chu khong phai thay the.
drop function if exists public.admin_list_metadata_games(text, text, boolean, bigint, boolean, int, text, boolean, jsonb, int);
drop function if exists public.admin_list_metadata_games(text, text, boolean, bigint, boolean, int, text, boolean, jsonb, int, boolean);

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
  p_published boolean default null
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

  execute format(
    'select coalesce(jsonb_agg(to_jsonb(t) - ''search_vector'' order by %s), ''[]''::jsonb)
       from (select * from public.metadata_games where %s order by %s limit %s) t',
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
    v_next := jsonb_build_object('value', v_last -> v_col, 'id', v_last -> 'id');
  end if;

  return jsonb_build_object('ok', true, 'data', jsonb_build_object(
    'items', v_rows,
    'nextCursor', v_next,
    'totalItems', v_total));
end;
$fn$;

revoke all on function public.admin_list_metadata_games(text, text, boolean, bigint, boolean, int, text, boolean, jsonb, int, boolean) from public, anon, authenticated;
grant execute on function public.admin_list_metadata_games(text, text, boolean, bigint, boolean, int, text, boolean, jsonb, int, boolean) to service_role;

-- == 13) Doc dieu kien + so lieu an/hien =========================

create or replace function public.admin_get_publish_rules()
returns jsonb
language sql
stable
security definer
set search_path = public, extensions
as $fn$
  select jsonb_build_object('ok', true, 'data', jsonb_build_object(
    'rules', public.metadata_publish_rules_json(r),
    'total', (select count(*) from public.metadata_games),
    'unpublishedTotal', (select count(*) from public.metadata_games where not is_published),
    'manuallyUnpublished', (select count(*) from public.metadata_games where manually_unpublished),
    'failingGate', (select count(*) from public.metadata_games where cardinality(gate_failures) > 0),
    -- So ban ghi TRUOT tung tieu chi, ke ca tieu chi dang TAT: admin phai thay
    -- duoc "bat cai nay se chan bao nhieu ban ghi" TRUOC khi bat.
    'wouldBlock', jsonb_build_object(
      'requireBanner',   (select count(*) from public.metadata_games where banner_url is null),
      'requirePoster',    (select count(*) from public.metadata_games where poster_url is null),
      'requireBackdrop',     (select count(*) from public.metadata_games where backdrop_url is null),
      'requireDeveloper',   (select count(*) from public.metadata_games where developer is null),
      'requirePublisher',   (select count(*) from public.metadata_games where publisher is null),
      'requirePublishDate', (select count(*) from public.metadata_games where publish_date is null),
      'requireMetaScore',   (select count(*) from public.metadata_games where meta_score is null),
      'requireAvgPlaytime', (select count(*) from public.metadata_games where avg_playtime_hours is null),
      'requireSummary',     (select count(*) from public.metadata_games where summary is null),
      'requireIgdbId',      (select count(*) from public.metadata_games where igdb_id is null),
      'requireDlcBase',     (select count(*) from public.metadata_games where is_dlc and base_metadata_game_id is null))
  ))
  from public.metadata_publish_rules r
  where r.id;
$fn$;

revoke all on function public.admin_get_publish_rules() from public, anon, authenticated;
grant execute on function public.admin_get_publish_rules() to service_role;
