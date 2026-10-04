-- Requires crawler_url and crawler_secret in Vault, and a successful first run.
begin;
create extension if not exists pg_cron;
create extension if not exists pg_net with schema extensions;
do $$ begin
  if not exists(select 1 from vault.decrypted_secrets where name='crawler_url'
    and decrypted_secret ~ '^https://[a-z]{20}\.supabase\.co/functions/v1/igdb-crawler$')
    or not exists(select 1 from vault.decrypted_secrets where name='crawler_secret' and length(decrypted_secret)>=32)
  then raise exception 'CRAWLER_VAULT_NOT_READY'; end if;
  if not exists(select 1 from crawler.runs where status='completed') then
    raise exception 'CRAWLER_FIRST_RUN_REQUIRED'; end if;
end $$;
select cron.schedule('questlog-igdb-crawler','15 */6 * * *', $job$
  select net.http_post(
    url := (select decrypted_secret from vault.decrypted_secrets where name='crawler_url'),
    headers := jsonb_build_object('Content-Type','application/json','x-crawler-secret',
      (select decrypted_secret from vault.decrypted_secrets where name='crawler_secret')),
    body := '{}'::jsonb, timeout_milliseconds := 120000)
  where (select enabled from crawler.state where singleton);
$job$);
-- The first successful run is manual. Make the next cron tick eligible so
-- later runs settle onto the six-hour UTC schedule instead of skipping once.
update crawler.state set next_run_at=now() where singleton=true;
commit;
