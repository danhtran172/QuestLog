# Implementation plan: shared Admin Integrations

> Design source: `docs/superpowers/specs/2026-10-03-admin-integrations-design.md`  
> Status: approved design; implementation plan

## Ground rules

- Treat `docs/openapi.yaml` and the backend DTO/endpoint implementation as backend-owned
  work. Regenerate the frontend API client; never hand-edit `MasterPlay.Web/src/lib/api/**`.
- Keep secrets in Supabase Vault. Neither browser code, settings JSON, logs, fixtures,
  nor API responses may contain a raw value.
- Preserve existing user work and do not fold unrelated working-tree changes into this
  feature.
- Keep crawler deployment/cron operations in `supabase/crawler/deploy.ps1`; the Admin
  Console does not receive a deploy button.

## 1. Establish the secure persistence boundary

**Files:**
- Add `supabase/migrations/<timestamp>_admin_integrations.sql`
- Add matching guarded rollback SQL under `supabase/rollback/`
- Extend `supabase/tests/crawler_sql_test.ts` or add focused SQL tests beside it

**Work:**

1. Create the private `integrations` schema, `integration_configs`,
   `integration_secret_refs`, and `integration_audit_events` tables defined by the
   approved design.
2. Enable RLS as defense in depth. Revoke public access from the schema, tables,
   `vault.secrets`, and `vault.decrypted_secrets`; do not grant `anon` or
   `authenticated` any select/execute privileges.
3. Add tightly scoped `SECURITY DEFINER` functions with a locked `search_path` for:
   creating/replacing a provider’s complete secret set through `vault.create_secret`
   or `vault.update_secret`; retrieving the active IGDB pair for the crawler role;
   and retrieving only safe integration summaries for the server role.
4. Validate provider and field names in SQL from fixed allow-lists. Require all fields
   of a provider in one replacement operation. Insert audit events in the same
   transaction as each state change.
5. Add an operation-lock mechanism keyed by provider so replacement, connection test,
   and Cloudinary image sync cannot race. Ensure unsuccessful writes retain the active
   revision.
6. Write migration tests that demonstrate ordinary API roles cannot inspect metadata,
   references, decrypted Vault secrets, or invoke crawler-only retrieval. Test an IGDB
   revision rollover returns only the new active pair to the crawler role.

## 2. Replace static/config credentials with injectable server-side providers

**Files:**
- Add `MasterPlay.Infrastructure/Integrations/Configuration/*`
- Refactor `MasterPlay.Infrastructure/Integrations/Rawg/RawgService.cs`
- Refactor `MasterPlay.Infrastructure/Integrations/Igdb/IgdbService.cs`
- Refactor `MasterPlay.Infrastructure/Integrations/Gemini/GeminiService.cs`
- Refactor `MasterPlay.Infrastructure/Integrations/Cloudinary/CloudinaryImageService.cs`
- Add a small Steam Web API credential/test client without changing `SteamImageService`
- Update `MasterPlay.Host/Program.cs` dependency registration

**Work:**

1. Introduce a backend-only `IIntegrationCredentialStore` and provider-specific typed
   credential records. Its Supabase implementation calls only the privileged server
   functions from step 1 and caches metadata/credentials briefly by revision; cache
   invalidation is revision-aware.
2. Replace the hardcoded RAWG key and Gemini fallback key immediately. Both providers
   obtain keys through the credential store and emit safe integration errors rather
   than console output containing upstream content.
3. Convert IGDB from static environment/config lookup to an injected client that can
   reset its Twitch token cache when the active integration revision changes.
4. Convert Cloudinary client construction to use the active credential revision. Do
   not use the existing static singleton across a credential change.
5. Add provider test adapters with a common result type. Each performs the agreed
   read-only operation and maps timeout, rate-limit, invalid-credential, and upstream
   errors to safe application codes.
6. Add a Steam Web API key test only. The existing Steam store/CDN image service stays
   independent because it does not use an application API key.
7. Use typed logs/audit context with provider, action, and error code only; add
   redaction coverage for exception/request logging.

## 3. Add admin API contract and host endpoints

**Files:**
- `docs/openapi.yaml`
- Regenerated `MasterPlay.Web/src/lib/api/**` output only
- `MasterPlay.Host/Dtos.cs`
- `MasterPlay.Host/Program.cs`
- Add focused host tests if a backend test project exists; otherwise create the smallest
  test project following the solution conventions before endpoint implementation

**Work:**

1. Define the discriminated provider request schemas and safe `IntegrationSummary` in
   OpenAPI. Document `GET /api/admin/integrations`, `PUT /api/admin/integrations/{provider}`,
   `POST /api/admin/integrations/{provider}/test`, and the two-stage Cloudinary sync
   endpoint.
2. Add matching DTOs and service registrations. Validate exact fields, input lengths,
   unknown provider keys, and replacement semantics before reaching Vault.
3. Map the endpoints using the existing `RequireAdmin` guard in `Program.cs`, matching
   the existing `/api/admin/metadata/**` pattern. Return 401/403 before any provider
   information to unauthenticated/non-admin callers.
4. On a successful save, return status metadata only. Test remains a separate request;
   write each test outcome to metadata and audit records.
5. Add provider-specific rate-limit policies and return a safe `429` error when a test
   or image sync exceeds quota. Normalize every upstream response before returning it.
6. Remove integration-secret concerns from `GET/PUT /api/settings`; retain only legacy
   non-secret settings. The Admin Integrations surface is the sole credential UI.

## 4. Make IGDB crawler consume the shared active revision

**Files:**
- `supabase/functions/igdb-crawler/handler.ts`
- `supabase/functions/igdb-crawler/deno.json` if a client dependency/configuration is needed
- `supabase/crawler/deploy.ps1`
- `docs/superpowers/specs/2026-10-02-igdb-crawler-design.md`
- `supabase/tests/crawler_sql_test.ts` and dedicated handler tests where supported

**Work:**

1. Replace `Deno.env` reads for IGDB client ID/secret with one call to the crawler-only
   RPC. Keep only crawler invocation authorization in Edge Function secrets.
2. Ensure the RPC call is internal, authenticated with the crawler’s server identity,
   and neither request URLs nor diagnostics can serialize its returned values.
3. Treat an absent/invalid active credential as a structured crawler run failure that
   is auditable but cannot reveal the secret.
4. Update the deploy script so it no longer uploads IGDB secrets to Edge Function
   secrets, while preserving explicit dry-run and `-EnableCron` behavior.
5. Update the prior crawler design and operator documentation to describe the shared
   credential source and the operational migration path.

## 5. Add Cloudinary preview and confirmed execution

**Files:**
- `MasterPlay.Infrastructure/Integrations/Cloudinary/CloudinaryImageService.cs`
- `MasterPlay.Host/Program.cs`
- `MasterPlay.Host/Dtos.cs`
- `docs/openapi.yaml`

**Work:**

1. Split the current all-at-once migration into a dry-run planner and an executor
   accepting only a server-generated preview token/revision. The dry run reads local
   candidates and reports counts without uploading or changing stored paths.
2. Bind the execution to the exact admin, credential revision, and a short expiry so a
   stale preview cannot execute after a replacement. Recompute/safely skip changed
   candidates at execution time.
3. Preserve the current overwrite behavior only after the second explicit operation;
   return aggregate and per-item safe failures, then record an audit event.
4. Add tests proving dry-run makes zero Cloudinary calls and execution cannot run
   without a valid preview/confirmation.

## 6. Build the Admin Console surface from generated contract types

**Files:**
- `MasterPlay.Web/src/lib/workspace.ts`
- `MasterPlay.Web/src/components/AdminConsoleView.tsx`
- Add `MasterPlay.Web/src/components/AdminIntegrationsSection.tsx`
- Add focused integration card/dialog components under `MasterPlay.Web/src/components/`
- Update `MasterPlay.Web/src/lib/i18n.tsx`
- Add `MasterPlay.Web/tests/adminIntegrations.test.mjs`

**Work:**

1. Add `integrations` to the admin workspace section list and its navigation icon;
   render `AdminIntegrationsSection` in `AdminConsoleView`.
2. Consume generated types/endpoints rather than adding handwritten domain types to
   `adminApi.ts`. Keep only UI state in components.
3. Render five provider cards with status, configured field badges, last test outcome,
   accessible live error text, save, and test controls. Secret inputs start empty after
   every fetch and after a successful replacement.
4. Provide Cloudinary’s two-step preview/confirm flow with server-issued summary data;
   do not compute candidate counts in the browser.
5. Add English/Vietnamese strings and keyboard/focus behavior consistent with the
   existing Admin Console. Avoid adding localStorage or direct Supabase calls.
6. Test rendering, admin error display, blank secret refresh state, busy-button state,
   and the confirmation gate with API mocks derived from generated schemas.

## 7. End-to-end security, migration, and release verification

**Files:**
- `docs/API_CONTRACT.md`
- `docs/PARITY_CHECKLIST.md`
- `supabase/README.md`
- Integration/crawler operator documentation touched above

**Work:**

1. Update contract and parity documentation with the new admin routes, explicit
   secret-response restriction, and the removal of integration credentials from
   settings.
2. Document the required external credential rotation. Rotate the currently exposed
   RAWG/Gemini credentials outside source control, then save replacements through the
   new Admin Console after deployment.
3. Verify a regular user and guest receive no provider metadata or mutation access.
   Inspect host and Edge Function logs after real tests to confirm credentials are
   absent.
4. Verify a saved IGDB revision is used by both host preview behavior and an actual
   scheduled crawler run without redeploying the function.
5. Execute `npm run typecheck`, `npm run lint`, and `npm run test` in `MasterPlay.Web`;
   run the .NET solution/build and Supabase migration/crawler test commands available
   in the repository. Record missing scripts or environment-bound checks explicitly.

## Delivery order

Complete steps 1–4 before wiring the UI. Step 5 can proceed after the credential store
is available, but must land before exposing the Cloudinary controls. Step 6 begins only
after OpenAPI generation is complete. Step 7 is the release gate.
