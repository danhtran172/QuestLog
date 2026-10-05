-- Apply after migration 20261004180000 and after storing gemini_exe_url and
-- gemini_exe_secret in Vault. The worker remains paused until explicitly enabled.
begin;
do $$ begin
  if not exists(select 1 from vault.decrypted_secrets where name='gemini_exe_url'
    and decrypted_secret ~ '^https://[a-z]{20}\.supabase\.co/functions/v1/gemini-exe-enricher$')
    or not exists(select 1 from vault.decrypted_secrets where name='gemini_exe_secret' and length(decrypted_secret)>=32)
  then raise exception 'GEMINI_EXE_VAULT_NOT_READY'; end if;
end $$;
select cron.schedule('questlog-gemini-exe-enricher','15 */6 * * *', $job$
  select net.http_post(
    url := (select decrypted_secret from vault.decrypted_secrets where name='gemini_exe_url'),
    headers := jsonb_build_object('Content-Type','application/json','x-gemini-exe-secret',
      (select decrypted_secret from vault.decrypted_secrets where name='gemini_exe_secret')),
    body := '{}'::jsonb, timeout_milliseconds := 120000)
  where (select enabled from gemini_exe.state where singleton);
$job$);
-- The first run is manual. Make the next cron tick eligible so later runs
-- settle onto the six-hour UTC schedule instead of skipping once.
update gemini_exe.state set next_run_at=now() where singleton;
commit;
