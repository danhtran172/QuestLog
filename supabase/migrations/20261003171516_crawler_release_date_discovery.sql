-- Discovery is about newly released titles, not IGDB's historical numeric ID
-- sequence. Keep a compound cursor because multiple titles can share one
-- release timestamp. `discovery_id` remains the ID tie-breaker for this cursor.
alter table crawler.state add column release_cursor_at bigint;
update crawler.state
  set release_cursor_at = extract(epoch from (current_date - interval '30 days'))::bigint,
      discovery_id = 0
  where singleton;
alter table crawler.state alter column release_cursor_at set not null;

create or replace function public.crawler_targets() returns jsonb language sql stable
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
    'discovery_id',(select discovery_id from crawler.state where singleton),
    'release_cursor_at',(select release_cursor_at from crawler.state where singleton));
$$;

-- The source Edge Function pages by IGDB's first_release_date and carries the
-- largest ID at the date boundary as a tie-breaker. This function advances both
-- cursor values atomically with inserts, so a timeout can be safely retried.
create function public.crawler_discover_release(
  p_run uuid,
  p_games jsonb,
  p_cursor_at bigint,
  p_cursor_id bigint,
  p_scanned_through_at bigint,
  p_scanned_through_id bigint
) returns jsonb language plpgsql set search_path = pg_catalog, public, crawler as $$
declare p jsonb; g public.metadata_games; n integer; old_at bigint; old_id bigint;
  v_release_at bigint; v_stage text := 'START';
begin
  perform crawler.fence(p_run);
  select inserted into n from crawler.runs where id=p_run;
  select release_cursor_at, discovery_id into old_at, old_id from crawler.state where singleton;
  if p_cursor_at<>old_at or p_cursor_id<>old_id then raise exception 'CRAWLER_CURSOR_CONFLICT'; end if;
  if n>=100 then return jsonb_build_object('inserted',n,'end',false,'paused',true,'releaseCursorAt',old_at,'cursor',old_id); end if;
  if pg_database_size(current_database()) >= (select max_database_bytes*0.9 from crawler.state) then
    update crawler.runs set report=report||'{"discovery_paused":"database_headroom"}' where id=p_run;
    return jsonb_build_object('inserted',n,'end',false,'paused',true,'releaseCursorAt',old_at,'cursor',old_id);
  end if;
  if jsonb_array_length(p_games)>100 then raise exception 'CRAWLER_BATCH_LIMIT'; end if;
  if p_scanned_through_at is null or p_scanned_through_id is null then
    update crawler.state set release_cursor_at=extract(epoch from (current_date-interval '30 days'))::bigint,
      discovery_id=0 where singleton;
    return jsonb_build_object('inserted',n,'end',true,'releaseCursorAt',(select release_cursor_at from crawler.state where singleton),'cursor',0);
  end if;
  if (p_scanned_through_at,p_scanned_through_id)<=(old_at,old_id) then raise exception 'CRAWLER_CURSOR_ORDER'; end if;
  for p in select value from jsonb_array_elements(p_games)
    order by (value->>'release_at')::bigint,(value->>'id')::bigint loop
    v_stage := 'GAME_VALIDATE';
    exit when n>=100;
    v_release_at := (p->>'release_at')::bigint;
    if v_release_at is null or v_release_at<=0
      or (v_release_at,(p->>'id')::bigint)<=(old_at,old_id)
      or (v_release_at,(p->>'id')::bigint)>(p_scanned_through_at,p_scanned_through_id) then
      raise exception 'CRAWLER_CURSOR_ORDER';
    end if;
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
  update crawler.state set release_cursor_at=p_scanned_through_at,discovery_id=p_scanned_through_id where singleton;
  update crawler.runs set inserted=n where id=p_run;
  return jsonb_build_object('inserted',n,'end',false,
    'releaseCursorAt',(select release_cursor_at from crawler.state where singleton),
    'cursor',(select discovery_id from crawler.state where singleton));
exception when cardinality_violation then
  raise exception 'CRAWLER_CARDINALITY_%', v_stage;
end $$;

revoke all on function public.crawler_discover_release(uuid,jsonb,bigint,bigint,bigint,bigint)
  from public, anon, authenticated;
grant execute on function public.crawler_discover_release(uuid,jsonb,bigint,bigint,bigint,bigint)
  to service_role;
