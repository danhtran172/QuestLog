-- Admin Console — day ba vai tro anh tu thu vien ca nhan len kho dung chung,
-- cho nhung game DA noi (`metadata_game_id` khac null).
--
-- TAI SAO KHONG DUNG `admin_publish_library`: ham do chi nhan game CHUA noi
-- (Host loc `MetadataGameId == null`), va khi gap ten trung no danh dau
-- `matched` roi bo qua. Sau khi thu vien da noi het len kho, no khong con
-- duong nao cham toi cac ban ghi ay nua.
--
-- QUY TAC MOT CHIEU: chi dien vao o DANG TRONG, khong bao gio ghi de.
-- Anh admin da dat trong console la mot quyet dinh; thu vien ca nhan khong
-- phai nguon su that cua kho dung chung. Ghi de la mot cua mot chieu — mat
-- roi thi khong co ban ghi nao de lan ve.
--
-- `revision` chi tang khi that su co gi doi, neu khong lan sua ke tiep cua
-- admin se dinh 409 vo co.
--
-- Trigger `metadata_games_apply_gate` tu tinh lai `gate_failures` va
-- `is_published` sau moi UPDATE, nen khong can goi
-- `metadata_recompute_published()` o day.

create or replace function public.admin_push_game_images(
  p_payload jsonb,
  p_actor uuid
)
returns jsonb
language plpgsql
security definer
set search_path = public, extensions
as $$
declare
  v_item        jsonb;
  v_id          bigint;
  v_before      public.metadata_games;
  v_after       public.metadata_games;
  v_banner      text;
  v_backdrop    text;
  v_poster      text;
  v_seen        int := 0;
  v_missing     int := 0;
  v_changed     int := 0;
  v_set_banner  int := 0;
  v_set_backdr  int := 0;
  v_set_poster  int := 0;
  v_published   int := 0;
  v_changed_ids jsonb := '[]'::jsonb;
begin
  if p_actor is null then
    return jsonb_build_object('ok', false, 'code', 'ACTOR_REQUIRED');
  end if;

  for v_item in
    select value from jsonb_array_elements(coalesce(p_payload -> 'games', '[]'::jsonb))
  loop
    v_seen := v_seen + 1;

    v_id := nullif(v_item ->> 'id', '')::bigint;
    if v_id is null then
      return jsonb_build_object('ok', false, 'code', 'SYNC_ID_REQUIRED');
    end if;

    select * into v_before from public.metadata_games where id = v_id;
    if not found then
      -- Ban ghi da bi xoa khoi kho tu lan noi truoc. Dem lai chu khong lam
      -- hong ca lo: mot game bien mat khong phai ly do chan 98 game con lai.
      v_missing := v_missing + 1;
      continue;
    end if;

    v_banner   := nullif(btrim(coalesce(v_item ->> 'banner_url', '')), '');
    v_backdrop := nullif(btrim(coalesce(v_item ->> 'backdrop_url', '')), '');
    v_poster   := nullif(btrim(coalesce(v_item ->> 'poster_url', '')), '');

    -- Chi giu lai gia tri nao THUC SU dien vao cho trong.
    if v_before.banner_url is not null and btrim(v_before.banner_url) <> '' then
      v_banner := null;
    end if;
    if v_before.backdrop_url is not null and btrim(v_before.backdrop_url) <> '' then
      v_backdrop := null;
    end if;
    if v_before.poster_url is not null and btrim(v_before.poster_url) <> '' then
      v_poster := null;
    end if;

    if v_banner is null and v_backdrop is null and v_poster is null then
      continue;
    end if;

    update public.metadata_games
    set banner_url   = coalesce(v_banner, banner_url),
        backdrop_url = coalesce(v_backdrop, backdrop_url),
        poster_url   = coalesce(v_poster, poster_url),
        revision     = revision + 1,
        updated_at   = now()
    where id = v_id
    returning * into v_after;

    v_changed := v_changed + 1;
    if v_banner   is not null then v_set_banner := v_set_banner + 1; end if;
    if v_backdrop is not null then v_set_backdr := v_set_backdr + 1; end if;
    if v_poster   is not null then v_set_poster := v_set_poster + 1; end if;

    -- Ban ghi vua vuot qua bo loc nho co du anh.
    if v_after.is_published and not v_before.is_published then
      v_published := v_published + 1;
    end if;

    v_changed_ids := v_changed_ids || jsonb_build_array(jsonb_build_object(
      'id', v_id,
      'name', v_after.name,
      'revision', v_after.revision,
      'setBanner', v_banner is not null,
      'setBackdrop', v_backdrop is not null,
      'setPoster', v_poster is not null,
      'nowPublished', v_after.is_published and not v_before.is_published));

    insert into public.metadata_audit_events (actor_user_id, entity, entity_id, action, before, after)
    values (p_actor, 'game', v_id, 'update',
            jsonb_build_object(
              'banner_url', v_before.banner_url,
              'backdrop_url', v_before.backdrop_url,
              'poster_url', v_before.poster_url),
            jsonb_build_object(
              'banner_url', v_after.banner_url,
              'backdrop_url', v_after.backdrop_url,
              'poster_url', v_after.poster_url,
              'source', 'push-images'));
  end loop;

  return jsonb_build_object('ok', true, 'data', jsonb_build_object(
    'seen', v_seen,
    'missing', v_missing,
    'changed', v_changed,
    'bannerFilled', v_set_banner,
    'backdropFilled', v_set_backdr,
    'posterFilled', v_set_poster,
    'newlyPublished', v_published,
    'games', v_changed_ids));
end;
$$;

revoke all on function public.admin_push_game_images(jsonb, uuid) from public;
revoke all on function public.admin_push_game_images(jsonb, uuid) from anon;
revoke all on function public.admin_push_game_images(jsonb, uuid) from authenticated;
grant execute on function public.admin_push_game_images(jsonb, uuid) to service_role;
