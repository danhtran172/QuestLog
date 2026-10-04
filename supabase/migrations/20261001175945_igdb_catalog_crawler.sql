-- Additive crawler state. No changes to client contracts or existing metadata IDs.
create schema if not exists crawler;
revoke all on schema crawler from public, anon, authenticated;
grant usage on schema crawler to service_role;

create table crawler.state (
  singleton boolean primary key default true check (singleton),
  enabled boolean not null default false,
  discovery_id bigint not null default 0,
  lease_id uuid,
  lease_until timestamptz,
  next_run_at timestamptz not null default '-infinity',
  max_database_bytes bigint not null default 471859200 check (max_database_bytes > 0)
);
insert into crawler.state(singleton) values(true);
create table crawler.runs (
  id uuid primary key,
  started_at timestamptz not null default now(),
  finished_at timestamptz,
  status text not null default 'running' check (status in ('running','completed','partial','failed','interrupted')),
  inserted integer not null default 0 check (inserted between 0 and 100),
  refreshed integer not null default 0 check (refreshed between 0 and 10),
  errors integer not null default 0,
  report jsonb not null default '{}'
);
create table crawler.sources (
  game_id bigint primary key references public.metadata_games(id) on delete cascade,
  payload jsonb,
  baseline jsonb,
  overrides text[] not null default '{}',
  checked_at timestamptz,
  missing_since timestamptz
);
create table crawler.queue (
  game_id bigint primary key references public.metadata_games(id) on delete cascade,
  payload jsonb not null,
  queued_at timestamptz not null default now(),
  attempts integer not null default 0,
  next_attempt_at timestamptz not null default now(),
  error_code text
);
create index crawler_queue_ready on crawler.queue(next_attempt_at, queued_at);
create index crawler_sources_checked on crawler.sources(checked_at);
alter table crawler.state enable row level security;
alter table crawler.runs enable row level security;
alter table crawler.sources enable row level security;
alter table crawler.queue enable row level security;
grant select, insert, update, delete on all tables in schema crawler to service_role;

-- Explicit edits stay protected even if a later source value happens to match.
create function crawler.mark_overrides() returns trigger language plpgsql
set search_path = pg_catalog, public, crawler as $$
declare k text; changed text[] := '{}';
begin
  if current_setting('questlog.crawler_write', true) = 'on' then return new; end if;
  if old.igdb_id is distinct from new.igdb_id then
    delete from crawler.queue where game_id=new.id;
    update crawler.sources set payload=null,baseline=null,checked_at=null,missing_since=null
      where game_id=new.id;
  end if;
  foreach k in array array['name','developer','publisher','summary','publish_date',
    'is_dlc','poster_url','series_id','base_metadata_game_id'] loop
    if to_jsonb(old)->k is distinct from to_jsonb(new)->k then
      changed := array_append(changed,k);
    end if;
  end loop;
  if cardinality(changed) > 0 then
    insert into crawler.sources(game_id,overrides) values(new.id,changed)
    on conflict(game_id) do update set overrides = (
      select array_agg(distinct x) from unnest(crawler.sources.overrides || excluded.overrides) x);
  end if;
  return new;
end $$;
create trigger metadata_crawler_overrides after update on public.metadata_games
for each row execute function crawler.mark_overrides();

create function crawler.fence(p_run uuid) returns void language plpgsql
set search_path = pg_catalog, public, crawler as $$
begin
  perform 1 from crawler.state where singleton and enabled
    and lease_id = p_run and lease_until > clock_timestamp() for update;
  if not found then raise exception 'CRAWLER_LEASE_LOST'; end if;
  if pg_database_size(current_database()) >= (select max_database_bytes from crawler.state) then
    raise exception 'CRAWLER_DATABASE_BUDGET';
  end if;
end $$;

create function public.crawler_targets() returns jsonb language sql stable
set search_path = pg_catalog, public, crawler as $$
  with upcoming as (
    select g.igdb_id, g.id, s.checked_at from public.metadata_games g
    left join crawler.sources s on s.game_id=g.id
    where g.igdb_id is not null and g.publish_date >= current_date
    order by s.checked_at asc nulls first, g.publish_date, g.id limit 250
  ), other_games as (
    select g.igdb_id, g.id, s.checked_at from public.metadata_games g
    left join crawler.sources s on s.game_id=g.id
    where g.igdb_id is not null and (g.publish_date < current_date or g.publish_date is null)
    order by s.checked_at asc nulls first, g.id limit 250
  ) select jsonb_build_object('ids',coalesce((select jsonb_agg(igdb_id) from (
    select * from upcoming union all select * from other_games) t),'[]'::jsonb),
    'discovery_id',(select discovery_id from crawler.state where singleton));
$$;

create function public.crawler_claim(p_run uuid) returns jsonb language plpgsql
set search_path = pg_catalog, public, crawler as $$
declare s crawler.state;
begin
  select * into s from crawler.state where singleton for update;
  if not s.enabled then return jsonb_build_object('skip','disabled'); end if;
  if s.lease_until > now() then return jsonb_build_object('skip','busy'); end if;
  if s.next_run_at > now() then return jsonb_build_object('skip','not_due'); end if;
  if pg_database_size(current_database()) >= s.max_database_bytes then
    return jsonb_build_object('skip','database_budget');
  end if;
  update crawler.runs set status='interrupted', finished_at=now() where status='running';
  delete from crawler.runs where finished_at < now()-interval '30 days';
  insert into crawler.runs(id) values(p_run);
  update crawler.state set lease_id=p_run, lease_until=now()+interval '3 minutes',
    next_run_at=now()+interval '5 hours 55 minutes' where singleton;
  return public.crawler_targets();
end $$;

-- One transaction records observations, queue payloads and rotation checkpoint.
create function public.crawler_observe(p_run uuid, p_ids jsonb, p_games jsonb)
returns void language plpgsql set search_path = pg_catalog, public, crawler as $$
declare g public.metadata_games; p jsonb; previous jsonb; queued jsonb;
begin
  perform crawler.fence(p_run);
  if jsonb_array_length(p_ids)>500 or jsonb_array_length(p_games)>500 then
    raise exception 'CRAWLER_BATCH_LIMIT';
  end if;
  for g in select * from public.metadata_games where igdb_id in (
    select value::bigint from jsonb_array_elements_text(p_ids)) loop
    select value into p from jsonb_array_elements(p_games) where (value->>'id')::bigint=g.igdb_id;
    select payload into previous from crawler.sources where game_id=g.id;
    select payload into queued from crawler.queue where game_id=g.id;
    -- Reject older snapshots, but allow relation changes at the same timestamp.
    if (p->>'updated_at')::bigint < greatest((previous->>'updated_at')::bigint,
        (queued->>'updated_at')::bigint) then continue; end if;
    insert into crawler.sources(game_id,checked_at,missing_since)
      values(g.id,now(),case when p is null then now() end)
    on conflict(game_id) do update set checked_at=now(), missing_since=
      case when p is null then coalesce(crawler.sources.missing_since,now()) end;
    if p is not null and (previous is distinct from p or
      (g.base_metadata_game_id is null and exists(select 1 from public.metadata_games
        where igdb_id=(p->>'parent_id')::bigint))) then
      insert into crawler.queue(game_id,payload) values(g.id,p)
      on conflict(game_id) do update set payload=excluded.payload;
    else
      -- Source reverted to its accepted value, or disappeared: do not apply a
      -- superseded queued snapshot on the next run.
      delete from crawler.queue where game_id=g.id;
    end if;
  end loop;
end $$;

-- Source-owned data only. The old row is locked and admin overrides merged here,
-- never in a client-side read/modify/write cycle.
create function crawler.apply_game(p_game bigint, p jsonb, p_created boolean default false) returns boolean
language plpgsql set search_path = pg_catalog, public, crawler as $$
declare g public.metadata_games; s crawler.sources; merged jsonb; incoming jsonb;
  k text; v_series bigint; v_base bigint; v_id bigint; item jsonb;
  before_tags jsonb; after_tags jsonb; changed boolean; updated public.metadata_games;
begin
  select * into g from public.metadata_games where id=p_game for update;
  select * into s from crawler.sources where game_id=p_game;
  if g.igdb_id is distinct from (p->>'id')::bigint then raise exception 'CRAWLER_ID_CHANGED'; end if;
  if length(btrim(p->>'name'))=0 or p->>'name' is null then raise exception 'CRAWLER_NAME_REQUIRED'; end if;
  if p->>'series' is not null then
    insert into public.metadata_series(name) values(p->>'series') on conflict(name) do nothing;
    select id into v_series from public.metadata_series where name=p->>'series';
  end if;
  select id into v_base from public.metadata_games where igdb_id=(p->>'parent_id')::bigint and id<>p_game;
  incoming := (p - array['id','updated_at','checksum','genres','themes','parent_id','series'])
    || jsonb_build_object('series_id',v_series,'base_metadata_game_id',v_base);
  merged := to_jsonb(g);
  foreach k in array array['name','developer','publisher','summary','publish_date',
      'is_dlc','poster_url','series_id','base_metadata_game_id'] loop
    if not (k=any(coalesce(s.overrides,'{}'))) and
      (merged->k='null'::jsonb or merged->>k='' or
       (s.baseline is not null and merged->k is not distinct from s.baseline->k)) then
      merged := jsonb_set(merged,array[k],coalesce(incoming->k,'null'::jsonb));
    end if;
  end loop;
  select jsonb_build_object('genres',(select jsonb_agg(genre_id order by genre_id) from public.metadata_game_genres where metadata_game_id=p_game),
    'themes',(select jsonb_agg(theme_id order by theme_id) from public.metadata_game_themes where metadata_game_id=p_game)) into before_tags;
  delete from public.metadata_game_genres where metadata_game_id=p_game;
  for item in select value from jsonb_array_elements(p->'genres') loop
    insert into public.metadata_genres(name) values(item->>'name') on conflict(name) do nothing;
    select id into v_id from public.metadata_genres where name=item->>'name';
    insert into public.metadata_game_genres values(p_game,v_id) on conflict do nothing;
  end loop;
  delete from public.metadata_game_themes where metadata_game_id=p_game;
  for item in select value from jsonb_array_elements(p->'themes') loop
    insert into public.metadata_themes(name) values(item->>'name') on conflict(name) do nothing;
    select id into v_id from public.metadata_themes where name=item->>'name';
    insert into public.metadata_game_themes values(p_game,v_id) on conflict do nothing;
  end loop;
  select jsonb_build_object('genres',(select jsonb_agg(genre_id order by genre_id) from public.metadata_game_genres where metadata_game_id=p_game),
    'themes',(select jsonb_agg(theme_id order by theme_id) from public.metadata_game_themes where metadata_game_id=p_game)) into after_tags;
  changed := merged is distinct from to_jsonb(g) or before_tags is distinct from after_tags;
  if changed or p_created then
    updated := jsonb_populate_record(g,merged);
    perform set_config('questlog.crawler_write','on',true);
    update public.metadata_games set name=updated.name, developer=updated.developer,
      publisher=updated.publisher, summary=updated.summary, publish_date=updated.publish_date,
      is_dlc=updated.is_dlc, poster_url=updated.poster_url, series_id=updated.series_id,
      base_metadata_game_id=updated.base_metadata_game_id, revision=g.revision+case when p_created then 0 else 1 end
    where id=p_game returning * into updated;
    perform set_config('questlog.crawler_write','off',true);
    insert into public.metadata_audit_events(entity,entity_id,action,before,after)
    values('game',p_game,case when p_created then 'create' else 'update' end,
      case when not p_created then to_jsonb(g)-'search_vector' end,
      (to_jsonb(updated)-'search_vector') || jsonb_build_object('source','igdb-crawler'));
  end if;
  insert into crawler.sources(game_id,payload,baseline,checked_at)
    values(p_game,p,incoming,now()) on conflict(game_id) do update set
      payload=excluded.payload,baseline=excluded.baseline,checked_at=now(),missing_since=null;
  return changed;
end $$;

create function public.crawler_consume(p_run uuid) returns jsonb language plpgsql
set search_path = pg_catalog, public, crawler as $$
declare q record; used integer; n integer:=0; v_errors integer:=0; total_errors integer;
begin
  perform crawler.fence(p_run);
  select refreshed into used from crawler.runs where id=p_run;
  for q in select x.* from crawler.queue x join public.metadata_games g on g.id=x.game_id
    where x.next_attempt_at<=now()
    order by (x.queued_at < now()-interval '7 days') desc,
      (coalesce(nullif(x.payload->>'publish_date','')::date,g.publish_date)>=current_date) desc nulls last,
      case when coalesce(nullif(x.payload->>'publish_date','')::date,g.publish_date)>=current_date
        then coalesce(nullif(x.payload->>'publish_date','')::date,g.publish_date) end asc nulls last,
      x.queued_at, x.game_id limit 50 loop
    exit when used+n>=10;
    begin
      perform crawler.apply_game(q.game_id,q.payload);
      delete from crawler.queue where game_id=q.game_id;
      n:=n+1;
    exception when invalid_text_representation or check_violation or not_null_violation
      or datetime_field_overflow or raise_exception then
      update crawler.queue set attempts=attempts+1, error_code=sqlstate,
        next_attempt_at=now()+ make_interval(hours=>least(24,6*(attempts+1))) where game_id=q.game_id;
      v_errors:=v_errors+1;
    end;
  end loop;
  update crawler.runs set refreshed=refreshed+n,errors=crawler.runs.errors+v_errors
    where id=p_run returning errors into total_errors;
  return jsonb_build_object('refreshed',used+n,'errors',total_errors,'pending',(select count(*) from crawler.queue));
end $$;

create function public.crawler_discover(p_run uuid,p_games jsonb,p_cursor bigint,p_scanned_through bigint)
returns jsonb language plpgsql set search_path = pg_catalog, public, crawler as $$
declare p jsonb; g public.metadata_games; n integer; old_cursor bigint;
  v_stage text := 'START';
begin
  perform crawler.fence(p_run);
  select inserted into n from crawler.runs where id=p_run;
  select discovery_id into old_cursor from crawler.state;
  if p_cursor<>old_cursor then raise exception 'CRAWLER_CURSOR_CONFLICT'; end if;
  if n>=100 then
    return jsonb_build_object('inserted',n,'end',false,'paused',true,'cursor',old_cursor);
  end if;
  if pg_database_size(current_database()) >= (select max_database_bytes*0.9 from crawler.state) then
    update crawler.runs set report=report||'{"discovery_paused":"database_headroom"}' where id=p_run;
    return jsonb_build_object('inserted',n,'end',false,'paused',true,'cursor',old_cursor);
  end if;
  if jsonb_array_length(p_games)>100 then raise exception 'CRAWLER_BATCH_LIMIT'; end if;
  if p_scanned_through is null then
    update crawler.state set discovery_id=0 where singleton=true;
    return jsonb_build_object('inserted',n,'end',true,'cursor',0);
  end if;
  if p_scanned_through<=old_cursor then raise exception 'CRAWLER_CURSOR_ORDER'; end if;
  for p in select value from jsonb_array_elements(p_games) order by (value->>'id')::bigint loop
    v_stage := 'GAME_VALIDATE';
    exit when n>=100;
    if (p->>'id')::bigint<=old_cursor or (p->>'id')::bigint>p_scanned_through then
      raise exception 'CRAWLER_CURSOR_ORDER';
    end if;
    v_stage := 'GAME_LOOKUP';
    select * into g from public.metadata_games where igdb_id=(p->>'id')::bigint;
    if not found then
      -- Matching names are ambiguous (remakes/editions); never auto-attach a legacy ID.
      if exists(select 1 from public.metadata_games where igdb_id is null and lower(name)=lower(p->>'name')) then
        update crawler.runs set report=jsonb_set(report,'{ambiguous_names}',
          to_jsonb(coalesce((report->>'ambiguous_names')::integer,0)+1)) ||
          jsonb_build_object('ambiguous_igdb_ids',coalesce(report->'ambiguous_igdb_ids','[]')||jsonb_build_array(p->'id')) where id=p_run;
      else
        v_stage := 'GAME_INSERT';
        insert into public.metadata_games(igdb_id,name,is_dlc)
          values((p->>'id')::bigint,p->>'name',coalesce((p->>'is_dlc')::boolean,false))
          on conflict(igdb_id) do nothing returning * into g;
        if found then
          v_stage := 'SOURCE_INSERT';
          insert into crawler.sources(game_id,baseline) values(g.id,to_jsonb(g));
          v_stage := 'APPLY_GAME';
          perform crawler.apply_game(g.id,p,true);
          n:=n+1;
        end if;
      end if;
    end if;
  end loop;
  -- Move past rejected source records too, otherwise one malformed IGDB record
  -- would poison this discovery page forever.
  update crawler.state set discovery_id=p_scanned_through where singleton=true;
  update crawler.runs set inserted=n where id=p_run;
  return jsonb_build_object('inserted',n,'end',false,'cursor',(select discovery_id from crawler.state));
exception when cardinality_violation then
  raise exception 'CRAWLER_CARDINALITY_%', v_stage;
end $$;

create function public.crawler_finish(p_run uuid,p_status text,p_report jsonb)
returns jsonb language plpgsql set search_path = pg_catalog, public, crawler as $$
declare result jsonb;
begin
  if p_status not in ('completed','partial','failed') then raise exception 'CRAWLER_STATUS_INVALID'; end if;
  perform 1 from crawler.state where lease_id=p_run and lease_until>clock_timestamp() for update;
  if not found then raise exception 'CRAWLER_LEASE_LOST'; end if;
  update crawler.runs set status=p_status,finished_at=now(),report=report||p_report where id=p_run;
  update crawler.state set lease_id=null,lease_until=null where singleton;
  select to_jsonb(r) into result from crawler.runs r where id=p_run;
  return result;
end $$;

-- SECURITY INVOKER: only the service role has private schema/table privileges.
revoke all on all functions in schema crawler from public,anon,authenticated;
grant execute on all functions in schema crawler to service_role;
revoke all on function public.crawler_targets(), public.crawler_claim(uuid),
  public.crawler_observe(uuid,jsonb,jsonb), public.crawler_consume(uuid),
  public.crawler_discover(uuid,jsonb,bigint,bigint), public.crawler_finish(uuid,text,jsonb)
  from public,anon,authenticated;
grant execute on function public.crawler_targets(), public.crawler_claim(uuid),
  public.crawler_observe(uuid,jsonb,jsonb), public.crawler_consume(uuid),
  public.crawler_discover(uuid,jsonb,bigint,bigint), public.crawler_finish(uuid,text,jsonb)
  to service_role;
