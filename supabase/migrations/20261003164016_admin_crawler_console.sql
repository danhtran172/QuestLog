-- Crawler console audit trail. These rows represent work the crawler actually
-- accepted in a run; source observations stay in `crawler.sources`.
create table crawler.run_items (
  run_id uuid not null references crawler.runs(id) on delete cascade,
  game_id bigint not null references public.metadata_games(id) on delete cascade,
  action text not null check (action in ('inserted', 'refreshed')),
  recorded_at timestamptz not null default now(),
  primary key (run_id, game_id, action)
);
create index crawler_run_items_run_page on crawler.run_items(run_id, recorded_at desc, game_id desc);
create index crawler_run_items_game_page on crawler.run_items(game_id, recorded_at desc);
alter table crawler.run_items enable row level security;
grant select, insert, update, delete on crawler.run_items to service_role;

-- Keep the existing worker entry points and quotas intact. The only new side
-- effect is a small, cascade-retained audit row after a game is accepted.
create or replace function public.crawler_consume(p_run uuid) returns jsonb language plpgsql
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
      insert into crawler.run_items(run_id,game_id,action)
        values(p_run,q.game_id,'refreshed') on conflict do nothing;
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

create or replace function public.crawler_discover(
  p_run uuid, p_games jsonb, p_cursor bigint, p_scanned_through bigint
) returns jsonb language plpgsql set search_path = pg_catalog, public, crawler as $$
declare p jsonb; g public.metadata_games; n integer; old_cursor bigint;
  v_stage text := 'START';
begin
  perform crawler.fence(p_run);
  select inserted into n from crawler.runs where id=p_run;
  select discovery_id into old_cursor from crawler.state;
  if p_cursor<>old_cursor then raise exception 'CRAWLER_CURSOR_CONFLICT'; end if;
  if n>=100 then return jsonb_build_object('inserted',n,'end',false,'paused',true,'cursor',old_cursor); end if;
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
    if (p->>'id')::bigint<=old_cursor or (p->>'id')::bigint>p_scanned_through then raise exception 'CRAWLER_CURSOR_ORDER'; end if;
    v_stage := 'GAME_LOOKUP';
    select * into g from public.metadata_games where igdb_id=(p->>'id')::bigint;
    if not found then
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
          insert into crawler.run_items(run_id,game_id,action)
            values(p_run,g.id,'inserted') on conflict do nothing;
          n:=n+1;
        end if;
      end if;
    end if;
  end loop;
  update crawler.state set discovery_id=p_scanned_through where singleton=true;
  update crawler.runs set inserted=n where id=p_run;
  return jsonb_build_object('inserted',n,'end',false,'cursor',(select discovery_id from crawler.state));
exception when cardinality_violation then
  raise exception 'CRAWLER_CARDINALITY_%', v_stage;
end $$;

-- Admin RPCs deliberately return the same {ok, code, data} envelope as the
-- existing metadata console. Only the Host service role can call them.
create or replace function public.admin_crawler_status() returns jsonb
language plpgsql stable set search_path = pg_catalog, public, crawler as $$
declare s crawler.state; v_bytes bigint; v_cron jsonb := null; v_last jsonb := null;
begin
  select * into s from crawler.state where singleton;
  v_bytes := pg_database_size(current_database());
  if to_regclass('cron.job') is not null then
    execute $sql$
      select jsonb_build_object('schedule', j.schedule, 'active', j.active,
        'lastRunStatus', d.status, 'lastRunAt', d.start_time)
      from cron.job j
      left join lateral (
        select status, start_time from cron.job_run_details
        where jobid=j.jobid order by start_time desc limit 1
      ) d on true
      where j.jobname='questlog-igdb-crawler' limit 1
    $sql$ into v_cron;
  end if;
  select jsonb_build_object('id',r.id,'startedAt',r.started_at,'finishedAt',r.finished_at,
    'status',r.status,'inserted',r.inserted,'refreshed',r.refreshed,'errors',r.errors,'report',r.report)
    into v_last from crawler.runs r order by r.started_at desc, r.id desc limit 1;
  return jsonb_build_object('ok',true,'code',null,'data',jsonb_build_object(
    'enabled',s.enabled,'leaseId',s.lease_id,'leaseUntil',s.lease_until,'nextRunAt',s.next_run_at,
    'discoveryId',s.discovery_id,'queueDepth',(select count(*) from crawler.queue),
    'trackedGames',(select count(*) from crawler.sources),'databaseBytes',v_bytes,
    'databaseCapBytes',s.max_database_bytes,'discoveryBudgetBytes',(s.max_database_bytes*0.9)::bigint,
    'discoveryHeadroomBytes',greatest(0,(s.max_database_bytes*0.9)::bigint-v_bytes),
    'cron',v_cron,'latestRun',v_last));
end $$;

create or replace function public.admin_set_crawler_enabled(p_enabled boolean) returns jsonb
language plpgsql set search_path = pg_catalog, public, crawler as $$
begin
  if p_enabled is null then return jsonb_build_object('ok',false,'code','CRAWLER_ENABLED_REQUIRED','data',null); end if;
  update crawler.state set enabled=p_enabled,
    lease_id=case when p_enabled then lease_id else null end,
    lease_until=case when p_enabled then lease_until else null end
    where singleton;
  return public.admin_crawler_status();
end $$;

create or replace function public.admin_trigger_crawler() returns jsonb
language plpgsql set search_path = pg_catalog, public, crawler as $$
declare v_url text; v_secret text; v_request bigint;
begin
  if not (select enabled from crawler.state where singleton) then
    return jsonb_build_object('ok',false,'code','CRAWLER_DISABLED','data',null);
  end if;
  if exists(select 1 from crawler.state where singleton and lease_until>clock_timestamp()) then
    return jsonb_build_object('ok',false,'code','CRAWLER_RUN_ACTIVE','data',null);
  end if;
  select decrypted_secret into v_url from vault.decrypted_secrets where name='crawler_url';
  select decrypted_secret into v_secret from vault.decrypted_secrets where name='crawler_secret';
  if v_url is null or v_secret is null then
    return jsonb_build_object('ok',false,'code','CRAWLER_SCHEDULER_NOT_READY','data',null);
  end if;
  update crawler.state set next_run_at=now() where singleton;
  select net.http_post(url:=v_url,
    headers:=jsonb_build_object('Content-Type','application/json','x-crawler-secret',v_secret),
    body:='{}'::jsonb,timeout_milliseconds:=120000) into v_request;
  return jsonb_build_object('ok',true,'code',null,'data',jsonb_build_object('requestId',v_request));
end $$;

create or replace function public.admin_list_crawler_runs(p_cursor jsonb default null, p_limit integer default 25) returns jsonb
language plpgsql stable set search_path = pg_catalog, public, crawler as $$
declare v_started timestamptz; v_id uuid; v_items jsonb; v_next jsonb; v_limit integer:=greatest(1,least(coalesce(p_limit,25),100));
begin
  begin
    if p_cursor is not null then v_started:=(p_cursor->>'startedAt')::timestamptz; v_id:=(p_cursor->>'id')::uuid; end if;
  exception when others then return jsonb_build_object('ok',false,'code','CRAWLER_CURSOR_INVALID','data',null); end;
  with page as (
    select r.* from crawler.runs r
    where p_cursor is null or (r.started_at,r.id)<(v_started,v_id)
    order by r.started_at desc,r.id desc limit v_limit+1
  ), visible as (select * from page order by started_at desc,id desc limit v_limit)
  select coalesce(jsonb_agg(jsonb_build_object('id',id,'startedAt',started_at,'finishedAt',finished_at,
      'status',status,'inserted',inserted,'refreshed',refreshed,'errors',errors,'report',report)
      order by started_at desc,id desc),'[]'::jsonb),
    (select jsonb_build_object('startedAt',started_at,'id',id) from page order by started_at desc,id desc offset v_limit limit 1)
  into v_items,v_next from visible;
  return jsonb_build_object('ok',true,'code',null,'data',jsonb_build_object('items',v_items,'nextCursor',v_next,'pageSize',v_limit));
end $$;

create or replace function public.admin_list_crawler_run_items(p_run uuid, p_cursor jsonb default null, p_limit integer default 30) returns jsonb
language plpgsql stable set search_path = pg_catalog, public, crawler as $$
declare v_recorded timestamptz; v_game bigint; v_items jsonb; v_next jsonb; v_limit integer:=greatest(1,least(coalesce(p_limit,30),100));
begin
  if not exists(select 1 from crawler.runs where id=p_run) then return jsonb_build_object('ok',false,'code','CRAWLER_RUN_NOT_FOUND','data',null); end if;
  begin
    if p_cursor is not null then v_recorded:=(p_cursor->>'recordedAt')::timestamptz; v_game:=(p_cursor->>'gameId')::bigint; end if;
  exception when others then return jsonb_build_object('ok',false,'code','CRAWLER_CURSOR_INVALID','data',null); end;
  with page as (
    select i.*,g.name,g.igdb_id,g.publish_date from crawler.run_items i join public.metadata_games g on g.id=i.game_id
    where i.run_id=p_run and (p_cursor is null or (i.recorded_at,i.game_id)<(v_recorded,v_game))
    order by i.recorded_at desc,i.game_id desc limit v_limit+1
  ), visible as (select * from page order by recorded_at desc,game_id desc limit v_limit)
  select coalesce(jsonb_agg(jsonb_build_object('gameId',game_id,'igdbId',igdb_id,'name',name,'publishDate',publish_date,
      'action',action,'recordedAt',recorded_at) order by recorded_at desc,game_id desc),'[]'::jsonb),
    (select jsonb_build_object('recordedAt',recorded_at,'gameId',game_id) from page order by recorded_at desc,game_id desc offset v_limit limit 1)
  into v_items,v_next from visible;
  return jsonb_build_object('ok',true,'code',null,'data',jsonb_build_object('items',v_items,'nextCursor',v_next,'pageSize',v_limit));
end $$;

create or replace function public.admin_list_crawler_games(p_cursor jsonb default null, p_limit integer default 30) returns jsonb
language plpgsql stable set search_path = pg_catalog, public, crawler as $$
declare v_checked timestamptz; v_game bigint; v_items jsonb; v_next jsonb; v_limit integer:=greatest(1,least(coalesce(p_limit,30),100));
begin
  begin
    if p_cursor is not null then v_checked:=(p_cursor->>'checkedAt')::timestamptz; v_game:=(p_cursor->>'gameId')::bigint; end if;
  exception when others then return jsonb_build_object('ok',false,'code','CRAWLER_CURSOR_INVALID','data',null); end;
  with page as (
    select s.game_id,g.igdb_id,g.name,g.publish_date,s.checked_at,s.missing_since,
      exists(select 1 from crawler.queue q where q.game_id=s.game_id) as queued
    from crawler.sources s join public.metadata_games g on g.id=s.game_id
    where p_cursor is null or (coalesce(s.checked_at,'-infinity'::timestamptz),s.game_id)<(v_checked,v_game)
    order by s.checked_at desc nulls last,s.game_id desc limit v_limit+1
  ), visible as (select * from page order by checked_at desc nulls last,game_id desc limit v_limit)
  select coalesce(jsonb_agg(jsonb_build_object('gameId',game_id,'igdbId',igdb_id,'name',name,'publishDate',publish_date,
      'checkedAt',checked_at,'missingSince',missing_since,'queued',queued) order by checked_at desc nulls last,game_id desc),'[]'::jsonb),
    (select jsonb_build_object('checkedAt',coalesce(checked_at,'-infinity'::timestamptz),'gameId',game_id) from page order by checked_at desc nulls last,game_id desc offset v_limit limit 1)
  into v_items,v_next from visible;
  return jsonb_build_object('ok',true,'code',null,'data',jsonb_build_object('items',v_items,'nextCursor',v_next,'pageSize',v_limit));
end $$;

revoke all on function public.admin_crawler_status(), public.admin_set_crawler_enabled(boolean),
  public.admin_trigger_crawler(), public.admin_list_crawler_runs(jsonb,integer),
  public.admin_list_crawler_run_items(uuid,jsonb,integer), public.admin_list_crawler_games(jsonb,integer)
  from public, anon, authenticated;
grant execute on function public.admin_crawler_status(), public.admin_set_crawler_enabled(boolean),
  public.admin_trigger_crawler(), public.admin_list_crawler_runs(jsonb,integer),
  public.admin_list_crawler_run_items(uuid,jsonb,integer), public.admin_list_crawler_games(jsonb,integer)
  to service_role;

-- Service role is intentionally denied direct access to `cron`, `vault` and
-- the private crawler schema. These narrow admin RPCs need the migration owner
-- only for those reads/writes; their fixed search_path prevents caller objects
-- from shadowing any referenced name.
alter function public.admin_crawler_status() security definer;
alter function public.admin_set_crawler_enabled(boolean) security definer;
alter function public.admin_trigger_crawler() security definer;
alter function public.admin_list_crawler_runs(jsonb,integer) security definer;
alter function public.admin_list_crawler_run_items(uuid,jsonb,integer) security definer;
alter function public.admin_list_crawler_games(jsonb,integer) security definer;
