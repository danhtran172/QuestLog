# Admin Integrations — shared secret configuration design

**Status:** Proposed; approved architecture, pending implementation-plan review  
**Date:** 2026-10-03  
**Scope:** Admin Console configuration for RAWG, IGDB, Steam, Cloudinary, and Gemini.

## 1. Purpose and invariants

QuestLog needs one administrator-managed configuration for external providers that is
shared by the hosted API and the scheduled IGDB metadata crawler. The current
configuration is fragmented across host configuration and Edge Function secrets;
changing an IGDB credential can therefore leave the crawler and host inconsistent.

The feature must preserve these invariants:

1. Credential values are accepted only over authenticated, admin-only endpoints and
   are never returned, logged, rendered after saving, placed in browser storage, or
   included in telemetry.
2. The client receives metadata only: configured fields, revision, updater, last test
   outcome, and safe error code. It never receives a raw credential, partial secret,
   or encrypted ciphertext.
3. A provider change is atomic: either all required values for that provider become
   the next configuration revision, or the existing configuration remains active.
4. Host provider calls and the IGDB crawler resolve the same active configuration.
5. All configuration, test, and operational actions are audit events. Provider error
   messages are normalized before presentation so upstream responses cannot leak a
   secret.
6. No feature silently deploys a Supabase function or changes its schedule. Deployment
   remains an explicit operator workflow in `supabase/crawler/deploy.ps1`.

## 2. Credential model

Credential fields are provider-specific and supplied as a complete replacement set.
An omitted required field is invalid; it never means “keep the old secret”. This
prevents accidentally combining a new public identifier with an old secret.

| Provider | Required configuration | Admin operation |
|---|---|---|
| RAWG | API key | Save; test connection |
| IGDB | Twitch client ID; Twitch client secret | Save; test token plus minimal IGDB query; crawler uses the active revision automatically |
| Steam | Steam Web API key | Save; test connection |
| Cloudinary | Cloud name; API key; API secret | Save; test connection; optional image migration dry-run and confirmed execution |
| Gemini | API key | Save; test connection |

Steam library synchronization is explicitly out of scope. It needs a separately
authorized Steam identity and user consent, not the application-wide Steam Web API
key. Gemini testing must use a non-generative, low-cost metadata/model capability.

## 3. Secret storage and access boundary

### 3.1 Supabase data model

Add a private `integrations` schema; it must not be exposed through the Supabase Data
API. It contains only non-secret metadata:

```text
integration_configs
  provider                 unique provider key
  active_revision          positive integer
  configured_field_names   required field names currently present
  last_test_status         never_tested | passed | failed
  last_tested_at           nullable timestamptz
  last_test_error_code     nullable safe application code
  updated_at, updated_by

integration_secret_refs
  provider, revision, field_name
  vault_secret_id          reference to Supabase Vault only

integration_audit_events
  id, provider, action, actor_user_id, occurred_at, revision, outcome, error_code
```

`integration_secret_refs` holds Vault IDs, never ciphertext or plaintext. It is in the
private schema and is accessible only to narrowly scoped database functions used by
the host and crawler. No `anon` or `authenticated` role receives a grant to these
tables, views, or functions. RLS is enabled as defense in depth, with no permissive
client policy.

All replacement, deletion, and metadata/audit writes happen in one transaction. Old
Vault entries are retained until the new revision is confirmed active; then they are
removed by the same privileged operation or an auditable cleanup job. A failed write
never leaves a metadata revision referring to absent secrets.

### 3.2 Privileged retrieval

The host resolves credentials through a backend-only repository; the browser cannot
call Supabase directly for this data. The IGDB Edge Function uses a dedicated,
non-public RPC with a narrowly granted server role to retrieve only the two IGDB
values for the active revision at invocation time. The function keeps no IGDB
credential in its environment, cache, logs, URL, or response.

The RPC must validate the caller’s service identity and provider argument internally,
must use a fixed provider allow-list, and must return a structured two-field record
only for `igdb`. It must not accept arbitrary schema/table/function identifiers or
return Vault records. Exact role grants and the function’s `search_path` will be
verified against the installed Supabase/Postgres version during implementation.

The crawler’s existing crawler-auth secret remains an Edge Function secret. This
separates crawler invocation authorization from the credentials used to call IGDB.

## 4. Backend contract

All routes are beneath `/api/admin/integrations` and enforce the admin role from the
JWT. UI workspace mode is not an authorization mechanism.

| Method | Path | Purpose |
|---|---|---|
| `GET` | `/api/admin/integrations` | List provider metadata and safe status only |
| `PUT` | `/api/admin/integrations/{provider}` | Replace that provider’s complete credential set |
| `POST` | `/api/admin/integrations/{provider}/test` | Run a read-only provider-specific connectivity test |
| `POST` | `/api/admin/integrations/cloudinary/image-sync` | Create a dry-run migration report or execute only after explicit confirmation |

The API contract will define a discriminated `IntegrationProvider` and provider-specific
request schemas. The generated frontend client is regenerated from `docs/openapi.yaml`;
`MasterPlay.Web/src/lib/api/**` is never edited manually.

Responses return a common `IntegrationSummary`:

```json
{
  "provider": "igdb",
  "configured": true,
  "configuredFields": ["clientId", "clientSecret"],
  "revision": 4,
  "lastTest": { "status": "passed", "testedAt": "2026-10-03T10:15:00Z", "errorCode": null },
  "updatedAt": "2026-10-03T10:14:00Z",
  "updatedBy": "admin-user-id"
}
```

The server returns safe codes such as `INTEGRATION_NOT_CONFIGURED`,
`INTEGRATION_CREDENTIAL_INVALID`, `INTEGRATION_RATE_LIMITED`,
`INTEGRATION_UPSTREAM_UNAVAILABLE`, `INTEGRATION_TEST_FAILED`, and
`INTEGRATION_OPERATION_CONFLICT`. It does not forward raw upstream bodies.

`PUT` validates the complete payload before it changes the active revision. A test is
not implicitly performed by Save: saving a known credential while a provider is rate
limited must remain possible, and the UI must show the separate test status.

## 5. Provider tests and operations

All tests execute server-side with short timeout, provider-specific rate limiting, and
redacted structured logging. Tests do not modify external data.

| Provider | Test |
|---|---|
| RAWG | Authenticated lightweight catalogue/read request |
| IGDB | Request Twitch client-credentials token, then execute a minimal IGDB read query |
| Steam | Authenticated lightweight Web API request that validates the key |
| Cloudinary | Authenticated, read-only Admin API ping/read request |
| Gemini | Read non-generative model metadata/list capability |

Cloudinary image migration is intentionally separate from “Test connection”. The
existing migration overwrites Cloudinary assets, so the new endpoint defaults to
`dryRun: true`, returns counts and candidate paths, and performs no upload until the
admin explicitly sends `dryRun: false` after reviewing the report. The execution
records an audit event and reports per-item failures without returning credentials.

The Admin Console does not expose crawler deployment, cron enablement, or a manual
run control. Those remain operator workflows until an independently designed,
authenticated operational surface is needed.

## 6. Admin Console experience

Add an **Integrations** section to Admin Console, not the per-user Settings dialog.
It renders five provider cards from `GET /api/admin/integrations`.

Each card displays provider name, required credential labels, configuration status,
last test result/time, and safe error message. Secret inputs are blank on every render;
the only indication of an existing value is its configured-field badge. Submitting a
card replaces its complete credential set. A separate **Test connection** control is
disabled while that card has an in-flight save or test.

The Cloudinary card additionally exposes **Preview image sync**. After a successful
dry-run it presents a summary and an explicit second action, **Sync N images**, before
the server receives `dryRun: false`. The UI does not make dry-run counts or image
migration decisions locally.

The UI uses generated OpenAPI types, i18n strings, accessible status text, and server
responses. It does not filter, sort, infer provider behavior, persist credentials, or
call Supabase directly.

## 7. Authorization, safety, and audit

1. Every endpoint checks authenticated admin authorization server-side before reading
   provider metadata or accepting a mutation.
2. Inputs use request-size caps and exact field validation. All secret-bearing request
   logging is disabled/redacted before structured logging or exception serialization.
3. Tests and Cloudinary execution use server-side rate limits and operation locks per
   provider to prevent quota abuse and concurrent replacement/sync races.
4. Audit events include actor, action, provider, revision, outcome, time, and safe error
   code; they never include credential values, request bodies, URLs containing keys, or
   upstream response bodies.
5. A failed test does not delete the active revision. A failed replacement leaves the
   preceding revision usable.
6. A future “clear credentials” action requires a separate confirmation and only
   removes QuestLog’s stored values. It does not revoke an external provider key.

## 8. Implementation boundaries and rollout

This is a cross-stack feature and must be delivered in these safe slices:

1. Backend-owned contract and database migration: private schema, Vault references,
   privilege model, RPC, audit records, and tests.
2. Host credential repository plus provider test services; replace the current hardcoded
   RAWG key and legacy host configuration reads only after the shared resolver works.
3. IGDB crawler change to resolve the active credential through the privileged path;
   update crawler deployment documentation, then verify scheduled invocation.
4. Admin API endpoints and generated OpenAPI client types.
5. Admin Console cards, status/error states, and Cloudinary dry-run/confirmation flow.
6. Security/operational verification and documentation updates, including the existing
   IGDB crawler design and parity checklist.

No raw credential may be copied into source, test snapshots, seed data, environment
example files, or browser fixtures. Existing exposed RAWG/Gemini credentials must be
rotated by an administrator outside this code change; the feature can securely store
their replacements but cannot revoke provider-side keys.

## 9. Verification and acceptance criteria

### Automated verification

- Database tests verify that public/regular authenticated roles cannot select secret
  metadata, secret references, Vault decrypted views, or the crawler-only RPC.
- API tests verify admin-only access; full-replacement validation; no raw secret in
  response/error/audit payload; revision atomicity; error normalization; and test
  rate/operation locking.
- Host tests use fake provider clients and assert each provider test is read-only.
- Crawler tests verify it retrieves only IGDB credentials, never logs them, and accepts
  a newly activated revision without redeployment.
- Frontend tests verify blank secret fields after refresh, correct card status, disabled
  in-flight controls, error display, and the Cloudinary dry-run confirmation gate.

### Manual/security verification

- Configure and test each provider using non-production test credentials where
  available, then inspect application and Edge Function logs for secret absence.
- Replace an IGDB credential and confirm both the host preview flow and scheduled
  crawler use the new revision without deployment.
- Confirm an ordinary user and a non-admin authenticated account both receive `403`
  from all `/api/admin/integrations/**` routes.
- Run a Cloudinary dry-run, verify no external asset changes, then execute a small
  confirmed batch and verify audit output and per-item failure handling.
- Run `npm run typecheck`, `npm run lint`, and `npm run test` in the frontend package;
  run the backend and Supabase migration test suites specified by their projects.

## 10. Non-goals

- Per-user provider credentials, Steam OAuth/library import, external-key revocation,
  generic arbitrary-provider support, automatic Edge Function deployment, and cron
  enablement are not included.
- This design does not change game-domain filters, sorting, provider ranking, or
  metadata merge rules.
