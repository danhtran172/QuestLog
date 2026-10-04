-- Admin Console — day thu vien ca nhan cua admin len kho metadata dung chung.
-- Spec: docs/superpowers/specs/2026-09-14-admin-console-mode-design.md §12.9.
--
-- Phu thuoc 202609140001 (metadata_audit_events) va 202609140002
-- (metadata_game_payload_error).
--
-- TAI SAO LA MOT HAM NHAN CA LO, khong phai 98 lan goi admin_create_metadata_game:
-- moi lan goi RPC la mot transaction rieng. Dut o ban ghi thu 60 se de kho co 59
-- ban ghi trong khi thu vien chua duoc noi ban nao — hai ben lech nhau va khong
-- co duong lui. Ca lo trong mot ham = mot transaction: hoac vao het, hoac khong
-- gi ca.
--
-- KHONG dong toi genre/theme: hai bang do thuoc tien trinh sync (spec §4.4).
-- Game o thu vien co the dang deo tag ca nhan, nhung tag ca nhan KHONG duoc
-- chay len kho dung chung.

create or replace function public.admin_publish_library(
  p_payload jsonb,
  p_actor uuid
)
returns jsonb
language plpgsql
security definer
set search_path = public, extensions
as $$
declare
  v_name        text;
  v_series_id   bigint;
  v_series_map  jsonb := '{}'::jsonb;   -- lower(ten series) -> id metadata
  v_game        jsonb;
  v_local       text;
  v_error       text;
  v_row         public.metadata_games;
  v_id_map      jsonb := '{}'::jsonb;   -- localId -> id metadata
  v_games_out   jsonb := '[]'::jsonb;
  v_matched     jsonb := '[]'::jsonb;
  v_base_local  text;
  v_base_id     bigint;
  v_series_new  int := 0;
  v_games_new   int := 0;
  v_base_linked int := 0;
  v_igdb_drop   int := 0;
begin
  if p_actor is null then
    return jsonb_build_object('ok', false, 'code', 'ACTOR_REQUIRED');
  end if;

  -- == 1) Series: tim theo ten, khong co thi tao =================
  -- Doi chieu khong phan biet hoa thuong de chay lai lan hai khong sinh ban sao.
  for v_name in
    select distinct btrim(value)
    from jsonb_array_elements_text(coalesce(p_payload -> 'series', '[]'::jsonb))
    where btrim(value) <> ''
  loop
    select id into v_series_id
    from public.metadata_series
    where lower(btrim(name)) = lower(v_name)
    limit 1;

    if v_series_id is null then
      -- `returning id`, KHONG phai `returning *`: v_row co kieu metadata_games.
      insert into public.metadata_series (name) values (v_name) returning id into v_series_id;
      v_series_new := v_series_new + 1;

      insert into public.metadata_audit_events (actor_user_id, entity, entity_id, action, before, after)
      values (p_actor, 'series', v_series_id, 'create', null,
              jsonb_build_object('id', v_series_id, 'name', v_name, 'source', 'publish-library'));
    end if;

    v_series_map := v_series_map || jsonb_build_object(lower(v_name), v_series_id);
  end loop;

  -- == 2) Games =================================================
  for v_game in
    select value from jsonb_array_elements(coalesce(p_payload -> 'games', '[]'::jsonb))
  loop
    v_local := v_game ->> 'localId';
    if v_local is null or btrim(v_local) = '' then
      return jsonb_build_object('ok', false, 'code', 'PUBLISH_LOCAL_ID_REQUIRED');
    end if;

    v_error := public.metadata_game_payload_error(v_game);
    if v_error is not null then
      return jsonb_build_object(
        'ok', false, 'code', v_error,
        'data', jsonb_build_object('localId', v_local, 'name', v_game ->> 'name'));
    end if;

    -- Ten da co trong kho -> NOI vao ban ghi san co thay vi tao ban sao.
    -- Kho dung chung khong co undo; tao trung la thiet hai kho go.
    select * into v_row
    from public.metadata_games
    where lower(btrim(name)) = lower(btrim(v_game ->> 'name'))
    limit 1;

    if found then
      v_id_map := v_id_map || jsonb_build_object(v_local, v_row.id);
      v_games_out := v_games_out || jsonb_build_array(jsonb_build_object(
        'localId', v_local, 'id', v_row.id, 'revision', v_row.revision, 'status', 'matched'));
      v_matched := v_matched || jsonb_build_array(jsonb_build_object(
        'localId', v_local, 'name', v_row.name, 'id', v_row.id));
      continue;
    end if;

    -- igdb_id la UNIQUE. Trung thi bo trong chu khong lam hong ca lo: ban ghi
    -- van len duoc, chi mat khoa doi chieu — dem lai va bao trong ket qua.
    if nullif(v_game ->> 'igdb_id', '') is not null
       and exists (select 1 from public.metadata_games
                   where igdb_id = (v_game ->> 'igdb_id')::bigint) then
      v_game := v_game - 'igdb_id';
      v_igdb_drop := v_igdb_drop + 1;
    end if;

    v_series_id := null;
    if nullif(btrim(coalesce(v_game ->> 'seriesName', '')), '') is not null then
      v_series_id := (v_series_map ->> lower(btrim(v_game ->> 'seriesName')))::bigint;
    end if;

    insert into public.metadata_games (
      igdb_id, name, developer, publisher, summary, publish_date, is_dlc,
      poster_url, backdrop_url, banner_url,
      meta_score, avg_playtime_hours, series_id)
    values (
      nullif(v_game ->> 'igdb_id', '')::bigint,
      btrim(v_game ->> 'name'),
      nullif(v_game ->> 'developer', ''),
      nullif(v_game ->> 'publisher', ''),
      nullif(v_game ->> 'summary', ''),
      nullif(v_game ->> 'publish_date', '')::date,
      coalesce(nullif(v_game ->> 'is_dlc', '')::boolean, false),
      nullif(v_game ->> 'poster_url', ''),
      nullif(v_game ->> 'backdrop_url', ''),
      nullif(v_game ->> 'banner_url', ''),
      nullif(v_game ->> 'meta_score', '')::int,
      nullif(v_game ->> 'avg_playtime_hours', '')::int,
      v_series_id)
    returning * into v_row;

    v_games_new := v_games_new + 1;
    v_id_map := v_id_map || jsonb_build_object(v_local, v_row.id);
    v_games_out := v_games_out || jsonb_build_array(jsonb_build_object(
      'localId', v_local, 'id', v_row.id, 'revision', v_row.revision, 'status', 'created'));

    insert into public.metadata_audit_events (actor_user_id, entity, entity_id, action, before, after)
    values (p_actor, 'game', v_row.id, 'create', null,
            to_jsonb(v_row) || jsonb_build_object('source', 'publish-library'));
  end loop;

  -- == 3) DLC -> ban goc ========================================
  -- Lam o luot RIENG: DLC co the duoc duyet truoc ban goc cua no, nen chi luot 2
  -- moi co du ban do localId -> id.
  for v_game in
    select value from jsonb_array_elements(coalesce(p_payload -> 'games', '[]'::jsonb))
    where value ->> 'baseLocalId' is not null
  loop
    v_local := v_game ->> 'localId';
    v_base_local := v_game ->> 'baseLocalId';
    v_base_id := (v_id_map ->> v_base_local)::bigint;

    if v_base_id is null then continue; end if;

    update public.metadata_games
    set base_metadata_game_id = v_base_id,
        revision = revision + 1,
        updated_at = now()
    where id = (v_id_map ->> v_local)::bigint
      and base_metadata_game_id is distinct from v_base_id
    returning * into v_row;

    if found then
      v_base_linked := v_base_linked + 1;

      -- Ban do tra ve phai mang revision MOI, neu khong lan sua ke tiep dinh 409.
      v_games_out := (
        select jsonb_agg(
          case when item ->> 'localId' = v_local
               then item || jsonb_build_object('revision', v_row.revision)
               else item end)
        from jsonb_array_elements(v_games_out) as t(item));

      insert into public.metadata_audit_events (actor_user_id, entity, entity_id, action, before, after)
      values (p_actor, 'game', v_row.id, 'update', null,
              jsonb_build_object('base_metadata_game_id', v_base_id, 'source', 'publish-library'));
    end if;
  end loop;

  return jsonb_build_object('ok', true, 'data', jsonb_build_object(
    'seriesCreated', v_series_new,
    'gamesCreated', v_games_new,
    'baseLinked', v_base_linked,
    'igdbIdDropped', v_igdb_drop,
    'matchedExisting', v_matched,
    'games', coalesce(v_games_out, '[]'::jsonb)));
end;
$$;

revoke all on function public.admin_publish_library(jsonb, uuid) from public;
revoke all on function public.admin_publish_library(jsonb, uuid) from anon;
revoke all on function public.admin_publish_library(jsonb, uuid) from authenticated;
grant execute on function public.admin_publish_library(jsonb, uuid) to service_role;
