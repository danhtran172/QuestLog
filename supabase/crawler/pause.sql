-- Reversible emergency stop. Preserve all metadata, queue, baselines and audit.
begin;
update crawler.state set enabled=false,lease_id=null,lease_until=null where singleton;
do $$ begin
  if to_regclass('cron.job') is not null then
    perform cron.unschedule(jobid) from cron.job where jobname='questlog-igdb-crawler';
  end if;
end $$;
commit;
