-- Reversible emergency stop. Keeps every stored executable candidate and run history.
begin;
update gemini_exe.state set enabled=false,lease_id=null,lease_until=null where singleton;
do $$ begin
  if to_regclass('cron.job') is not null then
    perform cron.unschedule(jobid) from cron.job where jobname='questlog-gemini-exe-enricher';
  end if;
end $$;
commit;
