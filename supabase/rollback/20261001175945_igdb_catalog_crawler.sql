-- SAFE rollback: disable execution, keep provenance and audit for recovery.
-- Does not reverse metadata already reconciled; inspect audit before any restore.
begin;
update crawler.state set enabled=false,lease_id=null,lease_until=null where singleton;
do $$ begin
  if to_regclass('cron.job') is not null then
    perform cron.unschedule(jobid) from cron.job where jobname='questlog-igdb-crawler';
  end if;
end $$;
commit;
