-- Discovery is keyed by IGDB release time; expose that checkpoint to the
-- admin console so the displayed state reflects the real scan order.
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
    'discoveryId',s.discovery_id,'releaseCursorAt',s.release_cursor_at,
    'queueDepth',(select count(*) from crawler.queue),
    'trackedGames',(select count(*) from crawler.sources),'databaseBytes',v_bytes,
    'databaseCapBytes',s.max_database_bytes,'discoveryBudgetBytes',(s.max_database_bytes*0.9)::bigint,
    'discoveryHeadroomBytes',greatest(0,(s.max_database_bytes*0.9)::bigint-v_bytes),
    'cron',v_cron,'latestRun',v_last));
end $$;

alter function public.admin_crawler_status() security definer;
revoke all on function public.admin_crawler_status() from public, anon, authenticated;
grant execute on function public.admin_crawler_status() to service_role;
