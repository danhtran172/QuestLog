# Admin Crawler Console Design

**Date:** 2026-10-03  
**Status:** Approved design; pending implementation plan

## Goal

Add a dedicated `Crawler` section to the Admin Console so an admin can inspect
the IGDB crawler's operating state, trigger or pause it safely, review each
run, and inspect the games that a run actually inserted or refreshed.

The section is operational metadata for the shared catalog. It is not part of
the catalog-quality Dashboard, which remains read-only.

## Scope

The section provides:

1. Current state: enabled/paused, active lease, next eligible run time, crawler
   discovery checkpoint, update queue count, database bytes, hard cap, and
   discovery headroom cap.
2. Scheduler state: active cron schedule and most recent scheduler outcome
   when available.
3. Run history: started/finished timestamps, duration, status, checked,
   inserted, refreshed, pending and errors; server-keyset-paginated.
4. Run details: server-keyset-paginated list of games actually inserted or
   refreshed by that run, including metadata id, IGDB id, name, poster URL,
   action, and completed timestamp.
5. Operations: `Run now`, and a safe enabled/paused toggle. The UI does not
   expose quota, cursor or cron-expression editing in this release.

There is no client-side filtering, sorting, quota calculation, credential
access, or direct Supabase call.

## User experience

`Crawler` is the fifth Admin Console section and is reachable through its tab
and `Ctrl+5`.

The top area has compact status tiles and a plain-language state badge. `Run
now` is disabled while a lease is active. Pausing requires confirmation because
it suppresses future cron invocations; it never deletes a queue, source
baseline, checkpoint, or historical run. Resuming only enables the worker and
does not bypass its database limit or lease fence.

The main content contains two tabs:

- **Runs** shows the server-ordered run history. Selecting a row opens its
  affected-game list inline below the row or in a detail panel on narrow
  screens.
- **Tracked games** shows crawler-managed metadata records with last checked
  time and source state. This is a paginated read-only diagnostic list; it does
  not pretend that all rows changed in a particular run.

Empty, loading and API-error states follow existing Admin Console patterns.
Errors are visible and retryable. No data is mocked.

## Data model

Existing `crawler.runs` continues to be the authoritative run aggregate.

Add `crawler.run_items`:

| Column | Meaning |
|---|---|
| `run_id` | FK to `crawler.runs`, cascade delete with the retained run |
| `game_id` | FK to `metadata_games` |
| `action` | `inserted` or `refreshed` only |
| `recorded_at` | when the crawler applied the source payload |

The table is append-only, has a primary key `(run_id, game_id, action)`, and is
indexed for `(run_id, recorded_at desc, game_id desc)` and tracked-game reads.
It intentionally stores only applied changes, not unchanged observations: this
keeps history bounded and makes the displayed count agree with `inserted` and
`refreshed`.

The existing 30-day retention cleanup for `crawler.runs` cascades to
`run_items`. Existing source records remain the source of truth for the
Tracked games tab.

## Database operations and security

New database functions live in the existing private crawler boundary and are
`SECURITY INVOKER`. They are executable only by `service_role`; `anon`,
`authenticated`, and `PUBLIC` have no access.

The Host API retains the existing `RequireAdmin` guard before it invokes those
functions with the service-role connection. The browser can never retrieve a
Vault secret or service-role key.

`Run now` is implemented server-side: it verifies that crawler state allows a
run, sets the next-run eligibility to now, and uses the existing Vault URL and
secret with `pg_net` to enqueue the Edge Function invocation. It returns an
accepted operation response rather than claiming that a long-running crawl has
already finished. The UI refreshes status and history after the request.

The crawler function records `run_items` in the same database transaction that
applies a new or refreshed metadata record. A failed transaction therefore
records neither the metadata write nor its alleged audit item.

## HTTP contract

All routes are under `/api/admin/metadata/crawler` and return `401`/`403` using
the Admin Console's current guard.

| Method | Path | Purpose |
|---|---|---|
| `GET` | `/api/admin/metadata/crawler` | Current crawler, database and scheduler state |
| `PUT` | `/api/admin/metadata/crawler/enabled` | Body `{ enabled }`; pause or resume safely |
| `POST` | `/api/admin/metadata/crawler/run` | Enqueue one eligible run; returns `202` |
| `GET` | `/api/admin/metadata/crawler/runs` | Keyset-paginated run history |
| `GET` | `/api/admin/metadata/crawler/runs/{runId}/games` | Keyset-paginated applied items for one run |
| `GET` | `/api/admin/metadata/crawler/games` | Keyset-paginated tracked-game diagnostics |

OpenAPI is changed first. `types.gen.ts` is regenerated from it and never
hand-edited. Host and frontend types trace directly to that generated contract.

## Host and frontend boundaries

`SupabaseMetadataAdmin` owns service-role RPC calls and PostgREST reads. New
Host DTOs map the contract and never expose raw crawler payloads, Vault values,
or database error text.

The Host performs server-side validation for page size, cursors, and the
enabled-body request. It maps crawler-specific outcomes to actionable HTTP
errors such as active lease, disabled state, unavailable scheduler, and stale
cursor.

The web app adds generated domain re-exports and `adminApi` methods, then a
single `AdminCrawlerSection`. React Query cache keys include the server cursor
and selected run id. It invalidates crawler status/history after the accepted
run or enabled-state mutation. It does not calculate duration caps, inspect
cursors, or aggregate counts itself; it only formats values sent by the API.

## Failure handling

- A run request rejected because the worker is active returns a visible
  conflict; it does not create a parallel invocation.
- A scheduler/Vault failure is shown as unavailable without leaking secrets.
- A pause/resume mutation reports its actual persisted state.
- A missing/deleted metadata record is omitted from tracked-game output rather
  than making historical list loading fail.
- Pagination cursors are opaque and rejected by the server if malformed.

## Verification

1. SQL tests cover `run_items` atomicity, retention cascade, keyset paging,
   service-role-only execution, pause/resume and manual-trigger fencing.
2. Host tests cover admin authorization, request validation, HTTP mapping and
   opaque cursor forwarding.
3. Web tests cover navigation, loading/error/empty states, mutation invalidation
   and rendering generated response fields.
4. Regenerate OpenAPI types; run web typecheck/tests and Host build.
5. Deploy migration and function, invoke one controlled run, and verify the
   history/detail APIs return the actual applied records.

## Explicit non-goals

- Editing cron cadence, database caps, source payloads or IGDB credentials in
  the browser.
- Bulk repair/edit actions from a crawler row.
- Storing every unchanged source observation as run history.
- Altering unrelated Admin Dashboard or publishing behavior.
