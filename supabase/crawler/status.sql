-- Read-only operational checks. No decrypted secrets returned.
select enabled,discovery_id,lease_until,next_run_at,
       pg_database_size(current_database()) as database_bytes,
       max_database_bytes, floor(max_database_bytes*0.9) as discovery_stop_bytes
from crawler.state;
select id,started_at,finished_at,status,inserted,refreshed,errors,report
from crawler.runs order by started_at desc limit 20;
select count(*) as queued, min(queued_at) as oldest_change,
       count(*) filter(where next_attempt_at>now()) as deferred
from crawler.queue;
select count(*) as linked, min(s.checked_at) as oldest_checked,
       count(*) filter(where s.checked_at is null) as never_checked,
       count(*) filter(where s.missing_since is not null) as source_missing
from public.metadata_games g left join crawler.sources s on s.game_id=g.id
where g.igdb_id is not null;
select g.igdb_id,q.attempts,q.error_code,q.next_attempt_at
from crawler.queue q join public.metadata_games g on g.id=q.game_id
where q.error_code is not null order by q.queued_at limit 50;
