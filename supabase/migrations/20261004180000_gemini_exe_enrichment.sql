-- Gemini EXE enrichment is deliberately separate from the IGDB crawler: a
-- filename prediction is a scanner hint, never shared canonical game metadata.
create schema if not exists gemini_exe;
revoke all on schema gemini_exe from public, anon, authenticated;
grant usage on schema gemini_exe to service_role;

create table if not exists gemini_exe.state (
  singleton boolean primary key default true check (singleton),
  enabled boolean not null default false,
  lease_id uuid,
  lease_until timestamptz,
  next_run_at timestamptz not null default '-infinity'
);
insert into gemini_exe.state(singleton) values(true) on conflict do nothing;

create table if not exists gemini_exe.runs (
  id uuid primary key,
  started_at timestamptz not null default now(), finished_at timestamptz,
  status text not null default 'running' check(status in ('running','completed','partial','failed','interrupted')),
  candidates integer not null default 0, no_candidates integer not null default 0,
  errors integer not null default 0, report jsonb not null default '{}'
);
create table if not exists public.metadata_game_executable_candidates (
  metadata_game_id bigint primary key references public.metadata_games(id) on delete cascade,
  file_name text, status text not null check(status in ('candidate','no_candidate')),
  source text not null default 'gemini' check(source='gemini'), model text,
  enriched_at timestamptz not null default now(), attempts integer not null default 0,
  last_error_code text, next_attempt_at timestamptz
);
create table if not exists gemini_exe.run_items (
  run_id uuid not null references gemini_exe.runs(id) on delete cascade,
  game_id bigint not null references public.metadata_games(id) on delete cascade,
  outcome text not null check(outcome in ('candidate','no_candidate','error')),
  file_name text, error_code text, recorded_at timestamptz not null default now(),
  primary key(run_id,game_id)
);
create index if not exists gemini_exe_runs_page on gemini_exe.runs(started_at desc,id desc);
create index if not exists gemini_exe_items_page on gemini_exe.run_items(run_id,recorded_at desc,game_id desc);
create index if not exists metadata_game_exe_candidates_page on public.metadata_game_executable_candidates(enriched_at desc,metadata_game_id desc);
alter table gemini_exe.state enable row level security;
alter table gemini_exe.runs enable row level security;
alter table gemini_exe.run_items enable row level security;
alter table public.metadata_game_executable_candidates enable row level security;
grant select,insert,update,delete on all tables in schema gemini_exe to service_role;
grant select,insert,update,delete on public.metadata_game_executable_candidates to service_role;

create or replace function gemini_exe.fence(p_run uuid) returns void language plpgsql set search_path=pg_catalog,gemini_exe as $$
begin
  perform 1 from gemini_exe.state where singleton and enabled and lease_id=p_run and lease_until>clock_timestamp() for update;
  if not found then raise exception 'GEMINI_EXE_LEASE_LOST'; end if;
end $$;

create or replace function public.gemini_exe_claim(p_run uuid) returns jsonb language plpgsql set search_path=pg_catalog,public,gemini_exe as $$
declare s gemini_exe.state; games jsonb;
begin
  select * into s from gemini_exe.state where singleton for update;
  if not s.enabled then return jsonb_build_object('skip','disabled'); end if;
  if s.lease_until>clock_timestamp() then return jsonb_build_object('skip','busy'); end if;
  if s.next_run_at>clock_timestamp() then return jsonb_build_object('skip','not_due'); end if;
  update gemini_exe.runs set status='interrupted',finished_at=now() where status='running';
  delete from gemini_exe.runs where finished_at<now()-interval '30 days';
  insert into gemini_exe.runs(id) values(p_run);
  update gemini_exe.state set lease_id=p_run,lease_until=now()+interval '3 minutes',next_run_at=now()+interval '6 hours' where singleton;
  select coalesce(jsonb_agg(jsonb_build_object('id',g.id,'name',g.name) order by g.created_at,g.id),'[]'::jsonb) into games
  from (select g.id,g.name,g.created_at from public.metadata_games g left join public.metadata_game_executable_candidates c on c.metadata_game_id=g.id
    where c.metadata_game_id is null or (c.next_attempt_at is not null and c.next_attempt_at<=now())
    order by g.created_at,g.id limit 10) g;
  return jsonb_build_object('runId',p_run,'games',games);
end $$;

create or replace function public.gemini_exe_record(p_run uuid,p_game bigint,p_file_name text,p_model text,p_error_code text default null) returns void language plpgsql set search_path=pg_catalog,public,gemini_exe as $$
declare outcome text; normalized text;
begin
  perform gemini_exe.fence(p_run);
  if p_file_name is not null and p_file_name !~ '^[^\\/:*?"<>|]+$' then raise exception 'GEMINI_EXE_FILENAME_INVALID'; end if;
  normalized:=nullif(lower(regexp_replace(btrim(p_file_name), '\.exe$','','i')),'');
  outcome:=case when p_error_code is not null then 'error' when normalized is null then 'no_candidate' else 'candidate' end;
  insert into gemini_exe.run_items(run_id,game_id,outcome,file_name,error_code) values(p_run,p_game,outcome,normalized,p_error_code);
  if outcome='error' then
    insert into public.metadata_game_executable_candidates(metadata_game_id,file_name,status,source,model,attempts,last_error_code,next_attempt_at)
      values(p_game,null,'no_candidate','gemini',p_model,1,p_error_code,now()+interval '6 hours')
    on conflict(metadata_game_id) do update set attempts=metadata_game_executable_candidates.attempts+1,last_error_code=excluded.last_error_code,next_attempt_at=now()+make_interval(hours=>least(24,6*(metadata_game_executable_candidates.attempts+1)));
    update gemini_exe.runs set errors=errors+1 where id=p_run;
  else
    insert into public.metadata_game_executable_candidates(metadata_game_id,file_name,status,source,model,enriched_at,attempts,last_error_code,next_attempt_at)
      values(p_game,normalized,outcome,'gemini',p_model,now(),0,null,null)
    on conflict(metadata_game_id) do update set file_name=excluded.file_name,status=excluded.status,source='gemini',model=excluded.model,enriched_at=now(),last_error_code=null,next_attempt_at=null;
    update gemini_exe.runs set candidates=candidates+(outcome='candidate')::int,no_candidates=no_candidates+(outcome='no_candidate')::int where id=p_run;
  end if;
end $$;

create or replace function public.gemini_exe_finish(p_run uuid,p_status text,p_report jsonb default '{}'::jsonb) returns jsonb language plpgsql set search_path=pg_catalog,gemini_exe as $$
begin
  perform gemini_exe.fence(p_run);
  if p_status not in ('completed','partial','failed') then raise exception 'GEMINI_EXE_STATUS_INVALID'; end if;
  update gemini_exe.runs set status=p_status,finished_at=now(),report=report||p_report where id=p_run;
  update gemini_exe.state set lease_id=null,lease_until=null where singleton;
  return (select to_jsonb(r) from gemini_exe.runs r where id=p_run);
end $$;

create or replace function public.gemini_exe_credentials() returns jsonb language sql security definer set search_path=pg_catalog,integrations as $$ select integrations.read_credentials('gemini') $$;

create or replace function public.admin_gemini_exe_status() returns jsonb language sql security definer stable set search_path=pg_catalog,public,gemini_exe as $$
 select jsonb_build_object('ok',true,'code',null,'data',jsonb_build_object('enabled',s.enabled,'leaseId',s.lease_id,'leaseUntil',s.lease_until,'nextRunAt',s.next_run_at,'queueDepth',(select count(*) from public.metadata_games g left join public.metadata_game_executable_candidates c on c.metadata_game_id=g.id where c.metadata_game_id is null or(c.next_attempt_at is not null and c.next_attempt_at<=now())),'latestRun',(select to_jsonb(r) from gemini_exe.runs r order by r.started_at desc,r.id desc limit 1))) from gemini_exe.state s where singleton $$;
create or replace function public.admin_set_gemini_exe_enabled(p_enabled boolean) returns jsonb language plpgsql security definer set search_path=pg_catalog,public,gemini_exe as $$ begin if p_enabled is null then return jsonb_build_object('ok',false,'code','GEMINI_EXE_ENABLED_REQUIRED','data',null); end if; update gemini_exe.state set enabled=p_enabled,lease_id=case when p_enabled then lease_id else null end,lease_until=case when p_enabled then lease_until else null end where singleton; return public.admin_gemini_exe_status(); end $$;
create or replace function public.admin_trigger_gemini_exe() returns jsonb language plpgsql security definer set search_path=pg_catalog,gemini_exe as $$ declare u text; secret text; request_id bigint; begin if not(select enabled from gemini_exe.state where singleton) then return jsonb_build_object('ok',false,'code','GEMINI_EXE_DISABLED','data',null); end if; if exists(select 1 from gemini_exe.state where singleton and lease_until>clock_timestamp()) then return jsonb_build_object('ok',false,'code','GEMINI_EXE_RUN_ACTIVE','data',null); end if; select decrypted_secret into u from vault.decrypted_secrets where name='gemini_exe_url'; select decrypted_secret into secret from vault.decrypted_secrets where name='gemini_exe_secret'; if u is null or secret is null then return jsonb_build_object('ok',false,'code','GEMINI_EXE_SCHEDULER_NOT_READY','data',null); end if; update gemini_exe.state set next_run_at=now() where singleton; select net.http_post(url:=u,headers:=jsonb_build_object('Content-Type','application/json','x-gemini-exe-secret',secret),body:='{}'::jsonb,timeout_milliseconds:=120000) into request_id; return jsonb_build_object('ok',true,'code',null,'data',jsonb_build_object('requestId',request_id)); end $$;
create or replace function public.admin_list_gemini_exe_runs(p_limit integer default 25) returns jsonb language sql security definer stable set search_path=pg_catalog,gemini_exe as $$ select jsonb_build_object('ok',true,'code',null,'data',jsonb_build_object('items',coalesce((select jsonb_agg(to_jsonb(r) order by r.started_at desc,r.id desc) from (select * from gemini_exe.runs order by started_at desc,id desc limit greatest(1,least(p_limit,100))) r),'[]'::jsonb),'nextCursor',null,'pageSize',greatest(1,least(p_limit,100)))) $$;
create or replace function public.admin_list_gemini_exe_games(p_limit integer default 30) returns jsonb language sql security definer stable set search_path=pg_catalog,public as $$ select jsonb_build_object('ok',true,'code',null,'data',jsonb_build_object('items',coalesce((select jsonb_agg(jsonb_build_object('gameId',g.id,'igdbId',g.igdb_id,'name',g.name,'fileName',g.file_name,'status',g.status,'source',g.source,'enrichedAt',g.enriched_at,'attempts',g.attempts,'lastErrorCode',g.last_error_code) order by g.enriched_at desc nulls last,g.id desc) from (select g.*,c.file_name,c.status,c.source,c.enriched_at,c.attempts,c.last_error_code from public.metadata_games g left join public.metadata_game_executable_candidates c on c.metadata_game_id=g.id order by c.enriched_at desc nulls last,g.id desc limit greatest(1,least(p_limit,100))) g),'[]'::jsonb),'nextCursor',null,'pageSize',greatest(1,least(p_limit,100)))) $$;
revoke all on function public.gemini_exe_claim(uuid),public.gemini_exe_record(uuid,bigint,text,text,text),public.gemini_exe_finish(uuid,text,jsonb),public.gemini_exe_credentials(),public.admin_gemini_exe_status(),public.admin_set_gemini_exe_enabled(boolean),public.admin_trigger_gemini_exe(),public.admin_list_gemini_exe_runs(integer),public.admin_list_gemini_exe_games(integer) from public,anon,authenticated;
grant execute on function public.gemini_exe_claim(uuid),public.gemini_exe_record(uuid,bigint,text,text,text),public.gemini_exe_finish(uuid,text,jsonb),public.gemini_exe_credentials(),public.admin_gemini_exe_status(),public.admin_set_gemini_exe_enabled(boolean),public.admin_trigger_gemini_exe(),public.admin_list_gemini_exe_runs(integer),public.admin_list_gemini_exe_games(integer) to service_role;
