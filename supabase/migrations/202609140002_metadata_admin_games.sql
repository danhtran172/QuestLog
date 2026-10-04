-- Admin Console — CRUD ban ghi metadata game.
-- Spec: docs/superpowers/specs/2026-09-14-admin-console-mode-design.md §7, §12.3, §12.7.4.
--
-- Phu thuoc 202609140001 (bang metadata_audit_events).
--
-- Cung khuon mau voi series: moi thao tac ghi la MOT ham plpgsql de ban ghi va
-- dong audit vao cung transaction, va tra ve jsonb `{ ok, code, data }` thay vi
-- raise exception.
--
-- KHONG dong toi genre/theme: `metadata_game_genres` / `_themes` thuoc tien trinh
-- sync (spec §4.4). Payload cua cac ham duoi day khong co hai field do.

-- ── Helper: doc payload jsonb thanh cac field co kieu ───────────
-- PUT la thay the toan bo, nen field vang mat = null. Chu y de khong nham voi
-- "khong doi".

create or replace function public.metadata_game_payload_error(p_payload jsonb)
returns text
language plpgsql
immutable
set search_path = public, extensions
as $$
declare
  v_name text := btrim(coalesce(p_payload ->> 'name', ''));
  v_meta int;
  v_avg int;
begin
  if v_name = '' then return 'GAME_NAME_REQUIRED'; end if;
  if length(v_name) > 300 then return 'GAME_NAME_TOO_LONG'; end if;

  v_meta := nullif(p_payload ->> 'meta_score', '')::int;
  if v_meta is not null and (v_meta < 0 or v_meta > 100) then
    return 'GAME_META_SCORE_RANGE';
  end if;

  v_avg := nullif(p_payload ->> 'avg_playtime_hours', '')::int;
  if v_avg is not null and v_avg < 0 then
    return 'GAME_AVG_PLAYTIME_RANGE';
  end if;

  return null;
end;
$$;

-- ── Games: create ───────────────────────────────────────────────

create or replace function public.admin_create_metadata_game(
  p_payload jsonb,
  p_actor uuid,
  p_confirm_duplicate boolean default false
)
returns jsonb
language plpgsql
security definer
set search_path = public, extensions
as $$
declare
  v_error text;
  v_name text := btrim(coalesce(p_payload ->> 'name', ''));
  v_igdb bigint := nullif(p_payload ->> 'igdb_id', '')::bigint;
  v_series bigint := nullif(p_payload ->> 'series_id', '')::bigint;
  v_base bigint := nullif(p_payload ->> 'base_metadata_game_id', '')::bigint;
  v_similar jsonb;
  v_row public.metadata_games;
begin
  v_error := public.metadata_game_payload_error(p_payload);
  if v_error is not null then
    return jsonb_build_object('ok', false, 'code', v_error);
  end if;

  -- Trung ten: CANH BAO chu khong chan cung. Remake/reboot trung ten la co that,
  -- nhung kho dung chung khong co undo nen phai bat admin xac nhan mot lan.
  if not coalesce(p_confirm_duplicate, false) then
    select jsonb_agg(jsonb_build_object('id', g.id, 'name', g.name, 'publishDate', g.publish_date))
      into v_similar
    from public.metadata_games g
    where lower(btrim(g.name)) = lower(v_name);

    if v_similar is not null then
      return jsonb_build_object('ok', false, 'code', 'GAME_NAME_SIMILAR', 'data', v_similar);
    end if;
  end if;

  if v_igdb is not null and exists (select 1 from public.metadata_games where igdb_id = v_igdb) then
    return jsonb_build_object('ok', false, 'code', 'GAME_IGDB_ID_TAKEN');
  end if;

  if v_series is not null and not exists (select 1 from public.metadata_series where id = v_series) then
    return jsonb_build_object('ok', false, 'code', 'GAME_SERIES_NOT_FOUND');
  end if;

  if v_base is not null and not exists (select 1 from public.metadata_games where id = v_base) then
    return jsonb_build_object('ok', false, 'code', 'GAME_BASE_NOT_FOUND');
  end if;

  insert into public.metadata_games (
    igdb_id, name, developer, publisher, summary, publish_date, is_dlc,
    base_metadata_game_id, poster_url, backdrop_url,
    banner_url, meta_score, avg_playtime_hours, series_id)
  values (
    v_igdb,
    v_name,
    nullif(p_payload ->> 'developer', ''),
    nullif(p_payload ->> 'publisher', ''),
    nullif(p_payload ->> 'summary', ''),
    nullif(p_payload ->> 'publish_date', '')::date,
    coalesce((p_payload ->> 'is_dlc')::boolean, false),
    v_base,
    nullif(p_payload ->> 'poster_url', ''),
    nullif(p_payload ->> 'backdrop_url', ''),
    nullif(p_payload ->> 'banner_url', ''),
    nullif(p_payload ->> 'meta_score', '')::int,
    nullif(p_payload ->> 'avg_playtime_hours', '')::int,
    v_series)
  returning * into v_row;

  insert into public.metadata_audit_events (actor_user_id, entity, entity_id, action, before, after)
  values (p_actor, 'game', v_row.id, 'create', null, to_jsonb(v_row) - 'search_vector');

  return jsonb_build_object('ok', true, 'data', to_jsonb(v_row) - 'search_vector');
end;
$$;

-- ── Games: update ───────────────────────────────────────────────

create or replace function public.admin_update_metadata_game(
  p_id bigint,
  p_payload jsonb,
  p_revision bigint,
  p_actor uuid
)
returns jsonb
language plpgsql
security definer
set search_path = public, extensions
as $$
declare
  v_error text;
  v_name text := btrim(coalesce(p_payload ->> 'name', ''));
  v_igdb bigint := nullif(p_payload ->> 'igdb_id', '')::bigint;
  v_series bigint := nullif(p_payload ->> 'series_id', '')::bigint;
  v_base bigint := nullif(p_payload ->> 'base_metadata_game_id', '')::bigint;
  v_before public.metadata_games;
  v_after public.metadata_games;
begin
  v_error := public.metadata_game_payload_error(p_payload);
  if v_error is not null then
    return jsonb_build_object('ok', false, 'code', v_error);
  end if;

  select * into v_before from public.metadata_games where id = p_id for update;
  if not found then
    return jsonb_build_object('ok', false, 'code', 'GAME_NOT_FOUND');
  end if;

  -- Optimistic concurrency. Tra ve ban ghi MOI NHAT de UI hien diff duoc, thay vi
  -- chi bao "co nguoi sua roi" roi bo mac admin doan.
  if p_revision is null or v_before.revision <> p_revision then
    return jsonb_build_object(
      'ok', false,
      'code', 'METADATA_REVISION_CONFLICT',
      'data', to_jsonb(v_before) - 'search_vector');
  end if;

  if v_igdb is not null and exists (
    select 1 from public.metadata_games where igdb_id = v_igdb and id <> p_id
  ) then
    return jsonb_build_object('ok', false, 'code', 'GAME_IGDB_ID_TAKEN');
  end if;

  if v_series is not null and not exists (select 1 from public.metadata_series where id = v_series) then
    return jsonb_build_object('ok', false, 'code', 'GAME_SERIES_NOT_FOUND');
  end if;

  if v_base is not null then
    if v_base = p_id then
      return jsonb_build_object('ok', false, 'code', 'GAME_BASE_SELF_REFERENCE');
    end if;
    if not exists (select 1 from public.metadata_games where id = v_base) then
      return jsonb_build_object('ok', false, 'code', 'GAME_BASE_NOT_FOUND');
    end if;
  end if;

  update public.metadata_games set
    igdb_id = v_igdb,
    name = v_name,
    developer = nullif(p_payload ->> 'developer', ''),
    publisher = nullif(p_payload ->> 'publisher', ''),
    summary = nullif(p_payload ->> 'summary', ''),
    publish_date = nullif(p_payload ->> 'publish_date', '')::date,
    is_dlc = coalesce((p_payload ->> 'is_dlc')::boolean, false),
    base_metadata_game_id = v_base,
    poster_url = nullif(p_payload ->> 'poster_url', ''),
    backdrop_url = nullif(p_payload ->> 'backdrop_url', ''),
    banner_url = nullif(p_payload ->> 'banner_url', ''),
    meta_score = nullif(p_payload ->> 'meta_score', '')::int,
    avg_playtime_hours = nullif(p_payload ->> 'avg_playtime_hours', '')::int,
    series_id = v_series,
    revision = v_before.revision + 1
  where id = p_id
  returning * into v_after;

  insert into public.metadata_audit_events (actor_user_id, entity, entity_id, action, before, after)
  values (
    p_actor, 'game', p_id, 'update',
    to_jsonb(v_before) - 'search_vector',
    to_jsonb(v_after) - 'search_vector');

  return jsonb_build_object('ok', true, 'data', to_jsonb(v_after) - 'search_vector');
end;
$$;

-- ── Games: delete ───────────────────────────────────────────────

create or replace function public.admin_delete_metadata_game(
  p_id bigint,
  p_actor uuid
)
returns jsonb
language plpgsql
security definer
set search_path = public, extensions
as $$
declare
  v_before public.metadata_games;
  v_dlc_count bigint;
begin
  select * into v_before from public.metadata_games where id = p_id for update;
  if not found then
    return jsonb_build_object('ok', false, 'code', 'GAME_NOT_FOUND');
  end if;

  -- FK la `on delete set null` nen DLC tro toi ban ghi nay se tu mo coi. Dem
  -- truoc de ghi vao audit — mat lien ket ma khong co dau vet la thu kho truy
  -- nguoc nhat ve sau.
  select count(*) into v_dlc_count
  from public.metadata_games where base_metadata_game_id = p_id;

  delete from public.metadata_games where id = p_id;

  insert into public.metadata_audit_events (actor_user_id, entity, entity_id, action, before, after)
  values (
    p_actor, 'game', p_id, 'delete',
    to_jsonb(v_before) - 'search_vector',
    jsonb_build_object('orphanedDlcCount', v_dlc_count));

  -- Thu vien local cua user KHONG bi dong toi: snapshot trong SQLite giu nguyen
  -- va `MetadataGameId` van tro vao id nay (spec §7).
  return jsonb_build_object('ok', true, 'data', jsonb_build_object('orphanedDlcCount', v_dlc_count));
end;
$$;

-- ── Bao mat ─────────────────────────────────────────────────────

revoke execute on function public.metadata_game_payload_error(jsonb) from public, anon, authenticated;
revoke execute on function public.admin_create_metadata_game(jsonb, uuid, boolean) from public, anon, authenticated;
revoke execute on function public.admin_update_metadata_game(bigint, jsonb, bigint, uuid) from public, anon, authenticated;
revoke execute on function public.admin_delete_metadata_game(bigint, uuid) from public, anon, authenticated;

grant execute on function public.metadata_game_payload_error(jsonb) to service_role;
grant execute on function public.admin_create_metadata_game(jsonb, uuid, boolean) to service_role;
grant execute on function public.admin_update_metadata_game(bigint, jsonb, bigint, uuid) to service_role;
grant execute on function public.admin_delete_metadata_game(bigint, uuid) to service_role;
