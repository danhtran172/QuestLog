# Gemini Executable Enrichment Design

**Date:** 2026-10-04  
**Status:** Proposed — approved for specification; awaiting implementation review

## Goal

Add a separately operated Gemini worker that progressively supplies a *candidate*
Windows executable filename for the oldest shared metadata games which have not
yet been enriched. The Admin Console gets a dedicated management section with the
same operational model as the IGDB Crawler Console.

The worker must never write a user's `Game.ExeName`, a launch path, `Ready`, or a
local filesystem path. A candidate is only input for the local scanner to verify
against a real installation later.

## Scope

The feature provides:

1. A private, scheduled Supabase Edge Function `gemini-exe-enricher`.
2. A database-owned state, exclusive lease, run history, candidate record, and
   item-level audit.
3. A fixed batch of **10** oldest eligible metadata games per run, scheduled every
   six hours. A manual run uses the same batch and only queues an invocation.
4. Admin-only HTTP endpoints and a dedicated **Gemini EXE** Admin Console section.
5. A normalized, provenance-bearing candidate (`source = gemini`), or a terminal
   `no_candidate` outcome when Gemini is not confident.

It does not add local scanning, Steam-library discovery, automatic assignment to a
library game, editing executable candidates in the browser, or a call to Gemini
from the IGDB crawler.

## Design choices

### Separate worker and schema

The worker uses a `gemini_exe` schema, rather than reusing `crawler`. The two
workers have different sources, retry rules, quotas, and operational ownership;
showing them as one pipeline would make a Gemini failure look like an IGDB
catalogue failure.

`gemini_exe.state` is a singleton containing `enabled`, `lease_id`, `lease_until`,
and `next_run_at`. `gemini_exe.runs` records `running`, `completed`, `partial`,
`failed`, or `interrupted` runs, counts for candidates, no-candidate results and
errors, and a redacted report. Its 30-day retention cascades to
`gemini_exe.run_items`.

`public.metadata_game_executable_candidates` has one current enrichment result per
`metadata_game_id`:

| Column | Meaning |
|---|---|
| `metadata_game_id` | Primary key and FK to the shared metadata record |
| `file_name` | Normalized basename without `.exe`; null for `no_candidate` |
| `status` | `candidate` or `no_candidate` |
| `source` | Currently fixed to `gemini` |
| `model` | Returned model identifier, never a credential |
| `enriched_at` | Successful terminal enrichment time |
| `attempts` / `last_error_code` / `next_attempt_at` | Retry diagnostics for transient failures |

The candidate table deliberately is not a column in `metadata_games`: it makes
provenance and retry state explicit, avoids treating an AI answer as canonical
game metadata, and leaves space for later verified store/manifests sources.

### Eligibility and execution

The database claims a run under the exclusive lease and selects the oldest
eligible rows in deterministic order:

```sql
metadata_games.created_at ASC, metadata_games.id ASC
```

An eligible row has no terminal candidate record and is not currently deferred by
`next_attempt_at`. A `candidate` and a confident `no_candidate` are both terminal;
the latter prevents an unanswerable title from consuming every run. Transport,
quota and timeout failures remain retryable with database-calculated exponential
backoff.

The Edge Function gets the active Gemini credential revision from the existing
server-side integrations store. It calls Gemini serially, with an eight-second
per-game timeout and a 90-second overall deadline. It asks only for a primary
Windows filename, validates it to a non-empty basename with a `.exe` suffix,
removes the suffix before storage, and records only a bounded error code. It logs
no prompt response, credential, full local path, or upstream body.

Ten serial calls fit inside the existing Edge runtime envelope while avoiding a
burst against Gemini. The function fences the lease before every database write;
pausing the worker prevents further items from being applied.

## Security and API boundary

The browser does not call the Edge Function and never receives the Gemini key.
All routes use the existing Host `RequireAdmin` guard and Host-to-Supabase
service-role boundary. New routes mirror the Crawler Console pattern:

| Method | Path | Purpose |
|---|---|---|
| `GET` | `/api/admin/metadata/gemini-exe` | Current state, scheduler and latest run |
| `PUT` | `/api/admin/metadata/gemini-exe/enabled` | Pause/resume safely with `{ enabled }` |
| `POST` | `/api/admin/metadata/gemini-exe/run` | Queue one eligible run; return `202 { requestId }` |
| `GET` | `/api/admin/metadata/gemini-exe/runs` | Server-keyset-paginated run history |
| `GET` | `/api/admin/metadata/gemini-exe/runs/{runId}/games` | Item-level outcomes for one run |
| `GET` | `/api/admin/metadata/gemini-exe/games` | Paginated candidate/enrichment diagnostics |

The Host validates opaque cursors and page sizes, maps operational conditions to
safe error codes, and never passes a raw database or provider response to the
client. Expected errors include disabled worker, active run, missing Gemini
integration, invalid cursor, missing run, and scheduler not ready.

## Admin Console

Add a separate `Gemini EXE` section after `Crawler` in `ADMIN_SECTIONS`. It uses
the same visual language and interaction model as `AdminCrawlerSection`:

- Status tiles show configured/enabled state, active lease, next run, eligible
  queue size, and the latest run summary.
- `Run now` sends only the queue request. It is disabled while paused, unconfigured
  or leased; pause has the same explicit confirmation treatment as Crawler.
- **Runs** displays server-ordered history. Selecting a run displays its
  candidate, no-candidate and error item outcomes.
- **Candidates** displays server-paginated metadata game diagnostics: name, IGDB
  ID, candidate filename or no-candidate state, source, last result and retry
  state. It is read-only and never infers ordering, queue size or progress.

All UI strings are i18n keys. Types are generated from `docs/openapi.yaml`; no
generated `src/lib/api/**` file is manually edited. The UI shows API failures and
offers retry using the existing admin error patterns.

## Data flow

```text
pg_cron / Admin Run now
  -> pg_net invokes gemini-exe-enricher
  -> database claims exclusive run + oldest 10 eligible metadata games
  -> Edge Function obtains server-side Gemini credential
  -> Gemini candidate / no-candidate / retryable failure
  -> transaction writes candidate state + run item + counters
  -> Host admin API exposes redacted state
  -> Gemini EXE Admin Console renders server results
  -> later, local scanner may verify a candidate against an installed file
```

## Failure and consistency rules

- A single game failure records a bounded code and allows the run to be `partial`;
  a lease/database failure fails the run.
- A run interrupted by pause or expiry writes no later item and cannot overwrite
  a completed candidate.
- Repeated manual requests while leased return a conflict rather than start a
  competing batch.
- Candidate writes, run-item audit and counters occur together in a database
  transaction. Run item history therefore never claims an enrichment that was not
  saved.
- The local scanner treats `candidate` as a hint only. It may set `Game.ExeName`
  only after confirming the file on that user's device; it never reports paths
  back to this worker.

## Verification

Tests must cover database claim ordering, terminal/no-candidate exclusion, retry
backoff, lease fencing, pause behavior, run-item atomicity, candidate normalization,
safe provider failures, Host admin authorization/error mapping, generated OpenAPI
types, and UI loading/empty/error/mutation states. No test fixture may contain a
real credential or filesystem path.

## Non-goals

- Replacing or changing the IGDB crawler schedule, quotas, storage budget or audit.
- Putting executable candidates into `metadata_games` as authoritative values.
- Launching a game, setting `Ready`, or writing a local user/device field from
  the shared worker.
- Browser editing, bulk approving or bulk retrying individual candidates in v1.
