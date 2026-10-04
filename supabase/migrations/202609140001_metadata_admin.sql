-- Admin Console — bien tap kho metadata dung chung.
-- Spec: docs/superpowers/specs/2026-09-14-admin-console-mode-design.md §6.2, §7.
--
-- Pham vi giai doan 2: audit trail + CRUD/merge cho `metadata_series`.
--
-- KHONG co ham ghi nao cho `metadata_genres` / `metadata_themes`: hai bang do
-- thuoc tien trinh sync (spec §4.4), khong ai sua tay.
--
-- Moi thao tac ghi la MOT ham plpgsql thay vi vai request PostgREST roi rac, vi
-- ban ghi nghiep vu va dong audit phai vao cung mot transaction. Kho dung chung
-- khong co undo — audit la duong truy vet duy nhat, mat dong audit la mat that.
--
-- Ham TRA VE jsonb `{ ok, code, data }` thay vi raise exception: PostgREST bien
-- exception thanh loi HTTP co cau truc khong on dinh, con day thi Host doc `code`
-- va map sang HTTP status mot cach tuong minh.

create table if not exists public.metadata_audit_events (
  id bigint generated always as identity primary key,
  actor_user_id uuid references auth.users(id) on delete set null,
  entity text not null,
  entity_id bigint,
  action text not null,
  before jsonb,
  after jsonb,
  created_at timestamptz not null default now(),
  constraint metadata_audit_events_entity_check
    check (entity in ('game', 'series', 'genre', 'theme')),
  constraint metadata_audit_events_action_check
    check (action in ('create', 'update', 'delete', 'merge'))
);

create index if not exists metadata_audit_events_entity_idx
  on public.metadata_audit_events (entity, entity_id, created_at desc);
create index if not exists metadata_audit_events_created_at_idx
  on public.metadata_audit_events (created_at desc);
create index if not exists metadata_audit_events_actor_idx
  on public.metadata_audit_events (actor_user_id, created_at desc);

-- Bao mat cua bang audit dat NGAY SAU khi tao bang, khong doi xuong cuoi file:
-- neu ai do chay script theo tung khuc thi bang va quyen cua no van di cung nhau,
-- khong bao gio co cua so bang da ton tai ma chua khoa.
--
-- Co tinh KHONG tao policy nao: chi service role doc duoc audit, va Host la noi
-- duy nhat giu service role key.
alter table public.metadata_audit_events enable row level security;

revoke insert, update, delete on public.metadata_audit_events from anon, authenticated;
revoke select on public.metadata_audit_events from anon, authenticated;

-- `metadata_genres` / `metadata_themes` chua co updated_at — them de tien trinh
-- sync sau nay doi soat duoc. `metadata_games` / `metadata_series` da co.
alter table public.metadata_genres
  add column if not exists updated_at timestamptz not null default now();
alter table public.metadata_themes
  add column if not exists updated_at timestamptz not null default now();

drop trigger if exists metadata_genres_set_updated_at on public.metadata_genres;
create trigger metadata_genres_set_updated_at
before update on public.metadata_genres
for each row execute function public.set_updated_at();

drop trigger if exists metadata_themes_set_updated_at on public.metadata_themes;
create trigger metadata_themes_set_updated_at
before update on public.metadata_themes
for each row execute function public.set_updated_at();

-- ── Series: create ──────────────────────────────────────────────

create or replace function public.admin_create_metadata_series(
  p_name text,
  p_actor uuid
)
returns jsonb
language plpgsql
security definer
set search_path = public, extensions
as $$
declare
  v_name text := btrim(coalesce(p_name, ''));
  v_row public.metadata_series;
begin
  if v_name = '' then
    return jsonb_build_object('ok', false, 'code', 'SERIES_NAME_REQUIRED');
  end if;

  -- Chan trung ngay o cua vao: kho dung chung khong co undo, don trung sau do
  -- ton mot thao tac merge thu cong.
  select * into v_row from public.metadata_series
  where lower(name) = lower(v_name)
  limit 1;
  if found then
    return jsonb_build_object(
      'ok', false,
      'code', 'SERIES_NAME_TAKEN',
      'data', to_jsonb(v_row));
  end if;

  insert into public.metadata_series (name) values (v_name) returning * into v_row;

  insert into public.metadata_audit_events (actor_user_id, entity, entity_id, action, before, after)
  values (p_actor, 'series', v_row.id, 'create', null, to_jsonb(v_row));

  return jsonb_build_object('ok', true, 'data', to_jsonb(v_row));
end;
$$;

-- ── Series: update ──────────────────────────────────────────────

create or replace function public.admin_update_metadata_series(
  p_id bigint,
  p_name text,
  p_actor uuid
)
returns jsonb
language plpgsql
security definer
set search_path = public, extensions
as $$
declare
  v_name text := btrim(coalesce(p_name, ''));
  v_before public.metadata_series;
  v_after public.metadata_series;
  v_clash public.metadata_series;
begin
  if v_name = '' then
    return jsonb_build_object('ok', false, 'code', 'SERIES_NAME_REQUIRED');
  end if;

  select * into v_before from public.metadata_series where id = p_id for update;
  if not found then
    return jsonb_build_object('ok', false, 'code', 'SERIES_NOT_FOUND');
  end if;

  select * into v_clash from public.metadata_series
  where lower(name) = lower(v_name) and id <> p_id
  limit 1;
  if found then
    return jsonb_build_object(
      'ok', false,
      'code', 'SERIES_NAME_TAKEN',
      'data', to_jsonb(v_clash));
  end if;

  update public.metadata_series set name = v_name where id = p_id returning * into v_after;

  insert into public.metadata_audit_events (actor_user_id, entity, entity_id, action, before, after)
  values (p_actor, 'series', p_id, 'update', to_jsonb(v_before), to_jsonb(v_after));

  return jsonb_build_object('ok', true, 'data', to_jsonb(v_after));
end;
$$;

-- ── Series: delete ──────────────────────────────────────────────

create or replace function public.admin_delete_metadata_series(
  p_id bigint,
  p_actor uuid
)
returns jsonb
language plpgsql
security definer
set search_path = public, extensions
as $$
declare
  v_before public.metadata_series;
  v_usage bigint;
begin
  select * into v_before from public.metadata_series where id = p_id for update;
  if not found then
    return jsonb_build_object('ok', false, 'code', 'SERIES_NOT_FOUND');
  end if;

  -- Khong tu go series khoi game dang gan. Xoa im lang la mat du lieu ma khong
  -- ai thay; admin phai merge hoac go tay truoc.
  select count(*) into v_usage from public.metadata_games where series_id = p_id;
  if v_usage > 0 then
    return jsonb_build_object(
      'ok', false,
      'code', 'SERIES_IN_USE',
      'data', jsonb_build_object('gameCount', v_usage));
  end if;

  delete from public.metadata_series where id = p_id;

  insert into public.metadata_audit_events (actor_user_id, entity, entity_id, action, before, after)
  values (p_actor, 'series', p_id, 'delete', to_jsonb(v_before), null);

  return jsonb_build_object('ok', true);
end;
$$;

-- ── Series: merge ───────────────────────────────────────────────

create or replace function public.admin_merge_metadata_series(
  p_source_id bigint,
  p_target_id bigint,
  p_actor uuid
)
returns jsonb
language plpgsql
security definer
set search_path = public, extensions
as $$
declare
  v_source public.metadata_series;
  v_target public.metadata_series;
  v_moved bigint;
begin
  if p_source_id = p_target_id then
    return jsonb_build_object('ok', false, 'code', 'SERIES_MERGE_SAME');
  end if;

  -- Khoa theo thu tu id tang dan de hai merge chay song song khong deadlock.
  perform * from public.metadata_series
  where id in (p_source_id, p_target_id)
  order by id
  for update;

  select * into v_source from public.metadata_series where id = p_source_id;
  if not found then
    return jsonb_build_object('ok', false, 'code', 'SERIES_NOT_FOUND');
  end if;
  select * into v_target from public.metadata_series where id = p_target_id;
  if not found then
    return jsonb_build_object('ok', false, 'code', 'SERIES_TARGET_NOT_FOUND');
  end if;

  update public.metadata_games set series_id = p_target_id where series_id = p_source_id;
  get diagnostics v_moved = row_count;

  delete from public.metadata_series where id = p_source_id;

  insert into public.metadata_audit_events (actor_user_id, entity, entity_id, action, before, after)
  values (
    p_actor,
    'series',
    p_target_id,
    'merge',
    to_jsonb(v_source),
    jsonb_build_object('target', to_jsonb(v_target), 'movedGames', v_moved));

  return jsonb_build_object(
    'ok', true,
    'data', jsonb_build_object('target', to_jsonb(v_target), 'movedGames', v_moved));
end;
$$;

-- ── Bao mat ─────────────────────────────────────────────────────
-- RLS cua bang metadata KHONG doi: `authenticated` van chi doc (spec §6.2).
-- Toan bo duong ghi di qua Host bang service role sau khi Host da xac thuc role
-- admin tu JWT.

revoke execute on function public.admin_create_metadata_series(text, uuid) from public, anon, authenticated;
revoke execute on function public.admin_update_metadata_series(bigint, text, uuid) from public, anon, authenticated;
revoke execute on function public.admin_delete_metadata_series(bigint, uuid) from public, anon, authenticated;
revoke execute on function public.admin_merge_metadata_series(bigint, bigint, uuid) from public, anon, authenticated;

grant execute on function public.admin_create_metadata_series(text, uuid) to service_role;
grant execute on function public.admin_update_metadata_series(bigint, text, uuid) to service_role;
grant execute on function public.admin_delete_metadata_series(bigint, uuid) to service_role;
grant execute on function public.admin_merge_metadata_series(bigint, bigint, uuid) to service_role;
