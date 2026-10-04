# IGDB catalog crawler

Approved scope: user delegated all remaining design decisions and deployment.

Supabase Edge Function `igdb-crawler`, triggered every six hours by pg_cron/pg_net.
Each successful run inserts at most 100 catalog games and processes at most 10
existing-game source updates. Updates run first, before discovery. These are
record budgets, not provider quotas. No RAWG, Steam, Gemini or image uploads.

## Discovery and refresh

Backfill uses ascending IGDB id and a persisted cursor; it restarts after reaching
the end to eventually revisit older IDs. Each run scans up to five pages of at
most 100 records, stopping at 100 inserts. Existing IDs never consume insert slots.
Existing catalog records with IGDB IDs are checked in two bounded groups: 250
upcoming (nearest release first among never-checked games) and 250 other records,
rotating by last check time. This detects changes to expanded relations even when
the parent game's `updated_at` does not change. Persisted payload comparisons
deduplicate observations; a source timestamp is never confused with local time.

Changed existing records enter a durable deduplicated queue. Pending >7 days take
priority (prevents starvation), then upcoming releases, nearest date, queue age.
Ten successful source reconciliations are reserved per run when available; fewer
is an honest partial result on insufficient changes, invalid data or outage.
Discovery cannot consume those ten slots. Checking 500 records does not mean
applying 500 updates. A larger catalog has a longer refresh rotation, reported
through queue age and last-check state. No claim of instantaneous change capture.

## Atomicity and recovery

Service-role-only RPCs, private state/queue/source/run tables with RLS. Claim a
3-minute lease, cap each worker at 100 seconds. Every write checks run UUID and
lease while locking state, fencing expired workers. Enqueue + check time and
insert + discovery cursor commit together. Replayed batches are idempotent.
Failed items retain payload, diagnostic code and bounded retry delay. Run counters
are database-enforced. HTTP retries only for provider 429/5xx, bounded and counted;
no blind retry of database mutations. A failed DB operation ends further IGDB calls.
Malformed IGDB records with valid IDs are counted as source errors and quarantined
individually, so one bad record cannot permanently poison a discovery checkpoint.

## Domain and editor compatibility

Only IGDB canonical fields: name, summary, developer, publisher, release date,
DLC/base relationship, series, genres/themes and poster. Never map IGDB ratings
to Metacritic, or screenshots to Steam/RAWG image roles. Initial reconciliation
of legacy records preserves populated fields with unknown provenance; subsequent
updates use a source baseline and persisted per-field overrides. Actual admin
changes after migration mark only the changed fields as overrides. Genre/theme
links are sync-owned. Manually unpublished and existing publishing gates remain
authoritative. Effective metadata changes increment revision exactly once and
write an audit event; timestamp-only source changes do not bump revision.

Unknown parent links are retained in source data and revisited through the update
queue. Missing IGDB records are flagged, never deleted from the shared catalog or
personal libraries. Name-only legacy duplicates are reported and left for review;
the worker does not guess the identity of remakes/editions. Source records are
matched only through IGDB ids.

## Security, resource limits and rollout

Dedicated >=32-character random bearer secret, checked before any DB/provider
work; stored in Edge secrets and Vault for cron. IGDB credentials are stored in the
shared Admin Integrations Vault configuration and resolved via a crawler-only RPC on
each invocation; they are never returned to a browser or logged. Token reuse is keyed
by the integration revision within an isolate. Up to
20 provider attempts/run, 1 request/second, sequential. Timeouts/caps include
retries. DB has a configurable size guard (default 450 MiB) and kill switch;
this is not a full organization billing quota monitor. Plan usage must be checked
in Dashboard. Crawler logs retained 30 days; queue bounded to catalog membership.

Deploy additive migration only, never replay old migrations into the live project.
Dry-run reads targets and calls IGDB without metadata/checkpoint writes. Enable
state and cron only after dry-run and first live run succeed. Authenticated CLI or
Management API access is required; service-role data keys cannot deploy functions.
