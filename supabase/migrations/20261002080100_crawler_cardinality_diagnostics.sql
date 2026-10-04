-- Preserve secret-safe diagnostics for unexpected production cardinality failures.
create or replace function public.crawler_discover(
  p_run uuid,
  p_games jsonb,
  p_cursor bigint,
  p_scanned_through bigint
) returns jsonb
language plpgsql
set search_path = pg_catalog, public, crawler
as $$
declare
  p jsonb;
  g public.metadata_games;
  n integer;
  old_cursor bigint;
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
      if exists(select 1 from public.metadata_games
        where igdb_id is null and lower(name)=lower(p->>'name')) then
        update crawler.runs set report=jsonb_set(report,'{ambiguous_names}',
          to_jsonb(coalesce((report->>'ambiguous_names')::integer,0)+1)) ||
          jsonb_build_object(
            'ambiguous_igdb_ids',
            coalesce(report->'ambiguous_igdb_ids','[]')||jsonb_build_array(p->'id')
          ) where id=p_run;
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
  update crawler.state set discovery_id=p_scanned_through where singleton=true;
  update crawler.runs set inserted=n where id=p_run;
  return jsonb_build_object(
    'inserted',n,
    'end',false,
    'cursor',(select discovery_id from crawler.state)
  );
exception when cardinality_violation then
  raise exception 'CRAWLER_CARDINALITY_%', v_stage;
end
$$;

revoke execute on function public.crawler_discover(uuid,jsonb,bigint,bigint)
  from public, anon, authenticated;
grant execute on function public.crawler_discover(uuid,jsonb,bigint,bigint)
  to service_role;
