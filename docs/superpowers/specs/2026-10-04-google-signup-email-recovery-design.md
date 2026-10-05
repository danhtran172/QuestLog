# Google Sign-up and Email-Code Account Recovery — design

**Status:** Implemented 2026-10-04 (pending Supabase migration + dashboard configuration, §13)  
**Date:** 2026-10-04  
**Scope:** Google sign-up/sign-in for Web and Desktop, forgot-password, and email-code
reauthentication for sensitive account changes.  
**Amends:** [`2026-08-24-account-multitenancy-design.md`](2026-08-24-account-multitenancy-design.md)
— the "Google identity linking" section and the reauthentication rules under
"Password, email, and username changes".

## 1. Purpose and invariants

QuestLog accounts can currently be created only with username, email, and password.
There is no forgot-password flow, and every sensitive account change requires the
current password. This design adds:

1. Sign-up and sign-in with a Google account on Hosted Web and the Desktop app.
2. Forgot-password through a 6-digit code sent by email.
3. One reauthentication mechanism for sensitive changes: a 6-digit code sent by email,
   for every account, whether it has a password or not.

Invariants:

1. The QuestLog Host remains the only auth gateway. The frontend never holds a Supabase
   refresh token, PKCE verifier, OAuth code, or email code beyond submitting the code
   once. No Supabase SDK is added to the frontend.
2. Every account has exactly one QuestLog profile and one library, regardless of how
   many identities (password, Google) Supabase links to it.
3. No account reaches library data without a username.
4. Public endpoints never reveal whether an account exists.
5. OAuth codes, `state`, PKCE verifiers, flow IDs, email codes, reauth tickets, and
   tokens are never logged, placed in a URL the Host controls, or returned twice.
6. Client input never chooses a role, an onboarding state, or a `has_password` value.

## 2. Confirmed product decisions

- A new Google user picks a username in QuestLog after Google authentication. Library
  access is blocked until the username is claimed.
- When the Google email matches an existing account with a **verified** email, Supabase
  automatic identity linking applies: the user enters the existing account. Supabase
  offers no switch to disable this; QuestLog records an audit event.
- The OAuth exchange is brokered by the Host (Authorization Code + PKCE). Desktop hands
  the session back to WebView2 through a secret `flowId` polled against the loopback
  Host.
- Email delivers **codes, not links**. Links pointing at `localhost` do not open from a
  phone, and PKCE links must be opened in the browser that started the flow, while the
  Desktop starts flows in WebView2 and mail opens elsewhere.
- Email-code reauthentication is the **only** reauthentication method for changing
  password, username, or email. `currentPassword` is removed from those endpoints.
  The single exception is the bootstrap administrator's forced first password change,
  which keeps `currentPassword` while `must_change_password = true`.
- Google identity linking/unlinking from settings with a *different* email, and Google
  as a reauthentication method, are out of scope (§15).

## 3. Current state

| Area | State before this design |
|---|---|
| `handle_new_auth_user` trigger | Raises `USERNAME_REQUIRED` when `raw_user_meta_data.username` is missing, so enabling the Google provider today would make every Google sign-up fail |
| `AuthConfig.googleEnabled` | Exposed by `GET /api/auth/config` and OpenAPI; no route or UI uses it |
| Forgot password | Not implemented |
| Sensitive changes | `change-password`, `account/username`, `account/email-change` require `currentPassword` |
| `must_change_password` gate | Enforced only by the frontend (`AuthGate.tsx`) |
| Desktop Shell | Loads the app from a loopback origin; already opens every non-loopback navigation and `window.open` in the system browser |
| Refresh token | `HttpOnly` cookie `questlog_refresh_token`, `Path=/api/auth`, set by the Host on the app's origin |
| Library ownership | Keyed by `user:{account.Id}`, not by username |

## 4. Data model

A new migration (the existing account-foundation migration is not edited) changes
`public.profiles`:

| Column | Change |
|---|---|
| `username`, `normalized_username` | Become nullable. Existing length/format checks stay; they pass on `NULL`. The unique constraint stays; multiple `NULL`s do not conflict |
| `onboarding_state` | New. `text not null default 'complete'`, check `in ('username_required', 'complete')` |
| `signup_method` | New. `text not null`, check `in ('password', 'google', 'bootstrap')`. Existing rows backfilled: bootstrap admin → `bootstrap`, others → `password` |
| `has_password` | New. `boolean not null`. Backfilled `true` for existing rows |

Additional constraint: `onboarding_state = 'complete'` implies `username is not null`.

`handle_new_auth_user` becomes:

| Condition | Profile created |
|---|---|
| `raw_user_meta_data.username` present | As today; `signup_method = 'password'` (the bootstrap path then sets `bootstrap`), `has_password = true`, `onboarding_state = 'complete'` |
| No username and `raw_app_meta_data.provider = 'google'` | `username = NULL`, `onboarding_state = 'username_required'`, `signup_method = 'google'`, `has_password = false` |
| Any other case | Raises `USERNAME_REQUIRED` as today |

A rollback script is added under `supabase/rollback/`.

## 5. Account lifecycle rules

Applied by the Host when a Google flow completes (§6.4), in this order:

1. **Locked account.** Same as password login: revoke the session just issued and
   return `423 ACCOUNT_LOCKED`.
2. **Pre-account-takeover residue.** If `signup_method = 'password'` but the user's
   identities no longer include `email`, Supabase has removed an *unconfirmed* password
   identity while linking Google. The username was chosen by someone who never proved
   control of the email. The Host:
   - resets the profile to `username = NULL`, `onboarding_state = 'username_required'`,
     `signup_method = 'google'`, `has_password = false`;
   - overwrites the auth password with a random value through the admin API, so a
     retained password hash cannot be used;
   - writes audit event `unverified_profile_reclaimed`.
   Unverified accounts could never sign in, so no library data exists to lose.
3. **Unfinished registration while registration is disabled.** If the profile is in
   `username_required` (a Google account that never completed registration, whether
   created in this flow, abandoned earlier, or reclaimed by rule 2) and
   `registrationEnabled = false`, the Host deletes the auth user through the admin API
   (the profile cascades) and returns `403 REGISTRATION_DISABLED`. Such a profile could
   not complete onboarding anyway (§7). Accounts that completed registration still sign
   in with Google. The rule depends on profile state, not on timestamps.
4. **Automatic link to a verified account.** If an `email` identity exists and the
   `google` identity's `created_at` falls within the flow lifetime (10 minutes) measured
   against the `Date` header of the Supabase token response, never the local clock,
   write audit event `google_identity_linked`.
5. **Otherwise** the session is issued normally.

Abandoned onboarding: a profile left in `username_required` stays as is. The next Google
sign-in resumes at username selection. No cleanup job in v1.

## 6. Google OAuth flow

### 6.1 Endpoints

| Method | Path | Auth | Purpose |
|---|---|---|---|
| `POST` | `/api/auth/google/start` | Public; Desktop needs desktop token | Body `{ client: "web" \| "desktop" }`. Returns `{ flowId, authorizeUrl, expiresAt }` |
| `GET` | `/api/auth/google/callback` | Public, bound by `state` | Query `code`, `state` (or `error`). Stores the code on the flow and returns a static page |
| `POST` | `/api/auth/google/complete` | Bound to flow | Body `{ flowId }`. Returns `{ status: "pending" }` or `{ status: "completed", session }` |

### 6.2 Sequence

```text
App  ── POST start ──────────────► Host: create flow {flowId, state, verifier, client, createdAt}
                                         TTL 10 min; Web: set questlog_oauth_flow cookie
App opens authorizeUrl
   Web: popup window
   Desktop: window.open → Shell opens the system browser (existing rule)
Browser ─► Supabase /auth/v1/authorize?provider=google&redirect_to=…&code_challenge=…&code_challenge_method=s256
        ─► Google ─► Supabase ─► Host GET /callback?code&state
                                         consume state (single use), store code on flow,
                                         return static "return to QuestLog" page
App  ── POST complete every 1.5 s ─► Host: first call after callback
                                         exchanges code + verifier (grant_type=pkce),
                                         applies §5, sets refresh cookie, returns AuthSession,
                                         deletes the flow
```

The code is exchanged at `complete`, not at the callback. A cancelled or abandoned flow
therefore never produces a session; the Supabase code simply expires after 5 minutes.
All outcomes are reported in the app, through i18n, in one place.

### 6.3 Binding a flow to its initiator

- **Web:** `start` sets cookie `questlog_oauth_flow = flowId` (`HttpOnly`, `Secure` when
  HTTPS, `SameSite=Lax`, `Path=/api/auth/google`, 10 minutes). The callback and
  `complete` require it to match. This prevents login CSRF and session swapping.
- **Desktop:** `start` and `complete` require `X-QuestLog-Desktop-Token` from a loopback
  address, reusing `DesktopGuestSession.AuthorizeExchange`. The callback runs in the
  system browser, which has no cookie and needs none; only WebView2 knows `flowId`.
- `state` and `flowId` are independent 256-bit random values. `state` travels through the
  browser and is single use; `flowId` appears only in the `start` response body.
- The callback ignores a `state` whose flow is unknown, expired, or already used, and
  returns the failure variant of the static page.

### 6.4 Completion

On the first `complete` after a successful callback the Host:

1. Calls `POST /auth/v1/token?grant_type=pkce` with `auth_code` and `code_verifier`.
2. Loads the profile and applies §5.
3. Sets `questlog_refresh_token` on the response (the app's own origin), returns
   `AuthSession`, and deletes the flow.

If the callback received `error` from Supabase, `complete` returns `OAUTH_CANCELLED`.
After expiry, `complete` returns `410 OAUTH_FLOW_EXPIRED`.

### 6.5 Configuration and limits

- `SUPABASE_GOOGLE_REDIRECT_URL`: the single callback URL for the deployment (development:
  `http://localhost:5173/api/auth/google/callback`). It is never derived from request
  headers. Supabase does not echo a client `state`, so the Host appends `?state=…` to this
  URL inside `redirect_to`; the Supabase allowlist entry is therefore the URL followed by
  `**`. If it is missing,
  `GET /api/auth/config` reports `googleEnabled: false` even when
  `SUPABASE_GOOGLE_ENABLED=true`.
- `start` returns `GOOGLE_DISABLED` when Google is not enabled.
- The callback returns a static page and never redirects, so it has no open-redirect
  surface. The page has no script and no token, and uses the browser's `Accept-Language`
  (it renders in the system browser, outside the app's settings).
- `start` uses the existing `auth` rate limiter. `complete` gets its own limiter
  (60 requests per minute per IP) because polling exceeds the `auth` limit.
- The flow store is in memory, holds at most 1,000 live flows, and purges expired ones.
  When full, `start` returns `429`. A Host restart loses pending flows; the user retries.
  A multi-instance Hosted deployment requires a shared store and is out of scope.

## 7. Username claim and onboarding gate

`POST /api/auth/account/username-claim { username }` (Bearer):

- Allowed only while `onboarding_state = 'username_required'`; otherwise
  `409 ONBOARDING_COMPLETE`.
- Requires `registrationEnabled = true`; completing a new account counts as registration.
- Validates exactly like `signup` (3–32 characters, `[A-Za-z0-9_.-]`, server
  normalization). No password or reauthentication is required.
- Updates with the filter `onboarding_state = eq.username_required`, so concurrent claims
  cannot both succeed. A duplicate returns `409 USERNAME_TAKEN`.
- On success: sets `onboarding_state = 'complete'`, runs the same new-account provisioning
  a verified password sign-up receives (default progress and ownership catalogs), writes
  audit event `username_claimed`, and returns `AuthAccount`.

**Server-side gate.** While `onboarding_state = 'username_required'`, the auth middleware
returns `403 USERNAME_REQUIRED` for every endpoint except `/api/auth/*`, `GET
/api/settings`, and `GET /api/capabilities`. Within `/api/auth/*`, `account/username`
and `account/email-change` also return `403 USERNAME_REQUIRED` during onboarding, so the
first username is set only through the claim. The frontend gate is a convenience, not
the enforcement.

## 8. Email codes

Supabase email templates "Reset Password" and "Magic Link" are changed to show
`{{ .Token }}` (6 digits, 10-minute expiry) instead of a link.

### 8.1 Forgot password (signed out)

- `POST /api/auth/password/forgot { identifier }` (public). `identifier` is a username or
  an email; the Host resolves a username to the email internally and calls
  `POST /auth/v1/recover`. Returns `202 { sent: true }` (also during the 60-second resend
  cooldown, when the previous code is still valid). **Amended 2026-10-04 (product decision):**
  an identifier with no account returns `404 ACCOUNT_NOT_FOUND`, and sign-up with an email that
  already has an account returns `409 EMAIL_TAKEN`. This trades enumeration resistance for clear
  feedback; the `auth` rate limit (10 requests/minute/IP) slows bulk probing.
- `POST /api/auth/password/verify-code { identifier, code }` (public):
  1. `POST /auth/v1/verify` with `type = recovery`, the resolved email, and the code.
     Supabase consumes the code here, and the recovery session it returns is revoked at once
     (`logout?scope=local`).
  2. A locked account returns `423`.
  3. Returns `{ resetTicket, expiresAt }`: 256-bit, hash-only in Host memory, bound to the
     verified account, 10 minutes, single use.
  A wrong or expired code returns `400 CODE_INVALID` and counts toward the 5-attempt limit.
- `POST /api/auth/password/reset { resetTicket, newPassword }` (public):
  1. Validates `newPassword` (at least 12 characters); a rejected password keeps the ticket.
  2. Sets the new password through the admin API, `has_password = true`, and
     `must_change_password = false`.
  3. Revokes the user's other sessions (`logout?scope=others`).
  4. Sets the refresh cookie and returns `AuthSession`; the user is signed in.
  An unknown, used, or expired ticket returns `401 RESET_TICKET_INVALID`.

A Google-only account can use this flow to set its first password while signed out.

### 8.2 Reauthentication (signed in)

- `POST /api/auth/reauth/email` (Bearer): calls `POST /auth/v1/otp` with the account's
  current email and `create_user = false`. Returns `202 { sent: true, resendAvailableAt }`.
  A request inside the 60-second cooldown returns `429 CODE_RESEND_COOLDOWN` with
  `resendAvailableAt`.
- `POST /api/auth/reauth/verify { code }` (Bearer):
  1. `POST /auth/v1/verify` with `type = email`.
  2. Requires the returned `user.id` to equal the signed-in account.
  3. Revokes the session that verification produced (`logout?scope=local` with that
     token), so the user keeps only their current session.
  4. Returns `{ reauthTicket, expiresAt }`.
- After 5 wrong codes for an account, verification is refused until a new code is
  requested (`429 CODE_ATTEMPTS_EXCEEDED`). The counter is in memory per account.

**Reauth ticket:** 256-bit random value. The Host stores only its hash, bound to the
account ID and the `session_id` claim of the access token that requested it. It lives
5 minutes and is consumed by the first successful use.

### 8.3 Sensitive endpoints

| Endpoint | New body |
|---|---|
| `POST /api/auth/change-password` | `{ newPassword, reauthTicket }`; for a Google-only account this sets the first password |
| `PATCH /api/auth/account/username` | `{ username, reauthTicket }` |
| `POST /api/auth/account/email-change` | `{ email, reauthTicket }`; confirmation of the new address keeps the current link-based Supabase email, which confirms server-side |

- A missing ticket returns `400 REAUTH_REQUIRED`. An invalid, expired, used, or
  wrongly bound ticket returns `401 REAUTH_INVALID`.
- **Exception:** while `must_change_password = true`, `change-password` accepts
  `{ currentPassword, newPassword }` instead. The Host checks this server-side; no other
  state accepts `currentPassword`.
- A successful password change sets `has_password = true`, writes audit event
  `password_set` or `password_changed`, and reissues the session as today.

## 9. API contract changes

`docs/openapi.yaml` is updated first; `MasterPlay.Web/src/lib/api/types.gen.ts` is then
regenerated, never edited by hand.

| Schema | Change |
|---|---|
| `AuthAccount` | `username` nullable; add `onboarding: "complete" \| "usernameRequired"` and `hasPassword: boolean` |
| `ChangePasswordRequest` | `currentPassword` optional (bootstrap exception only); add `reauthTicket` |
| `ChangeUsernameRequest`, `ChangeEmailRequest` | Replace `currentPassword` with `reauthTicket` |
| New | `GoogleStartRequest`, `GoogleStartResponse`, `GoogleCompleteRequest`, `GoogleCompleteResponse`, `UsernameClaimRequest`, `ForgotPasswordRequest`, `ForgotPasswordResponse`, `ResetPasswordRequest`, `ReauthCodeResponse`, `ReauthVerifyRequest`, `ReauthTicketResponse` |

New public endpoints: `google/start`, `google/callback`, `google/complete`,
`password/forgot`, `password/verify-code`, `password/reset`.

## 10. Error codes

| Code | HTTP | When |
|---|---|---|
| `GOOGLE_DISABLED` | 403 | Google sign-in is not enabled or not fully configured |
| `OAUTH_FLOW_EXPIRED` | 410 | Flow unknown, expired, or already completed |
| `OAUTH_CANCELLED` | 400 | The user cancelled at Google, or Supabase returned an error |
| `USERNAME_REQUIRED` | 403 | Domain endpoint called before the username is claimed |
| `ONBOARDING_COMPLETE` | 409 | Username claim on a completed profile |
| `CODE_INVALID` | 400 | Wrong or expired email code |
| `CODE_RESEND_COOLDOWN` | 429 | Reauth code requested again within 60 seconds |
| `CODE_ATTEMPTS_EXCEEDED` | 429 | 5 wrong reauth codes; request a new code |
| `REAUTH_REQUIRED` | 400 | Sensitive change without a ticket |
| `REAUTH_INVALID` | 401 | Ticket invalid, expired, used, or bound to another session |

Reused: `REGISTRATION_DISABLED`, `ACCOUNT_LOCKED`, `USERNAME_TAKEN`, `INVALID_USERNAME`,
`AUTH_PROVIDER_ERROR`, `AUTH_NOT_CONFIGURED`. All use the existing error envelope.

## 11. User experience

**Signed-out screens**

- Sign in and Create account both show **Continue with Google** when
  `configured && googleEnabled`. The Google mark is a bundled SVG; nothing is loaded from
  an external URL.
- Waiting state: "Waiting for you to finish in your browser…" (`aria-live="polite"`), with
  **Open Google again** and **Cancel** (stops polling). On Web, if the popup is blocked,
  a button opens it on a user gesture.
- Polling retries a network or 5xx failure up to 3 times, then shows the error with
  **Try again**.
- Sign in has **Forgot password?** → identifier → **Send code** ("If an account exists, a code is
  on its way"; the server answers 202 either way) → code, checked immediately through
  `POST /api/auth/password/verify-code`, which returns a single-use reset ticket (10 minutes) →
  new password + confirmation → signed in. Six digits typed or pasted submit the code step.

**Username selection** (in `AuthGate`, before the `mustChangePassword` check): the Google
email read-only, a username field with the format rules, **Continue**, and **Sign out**.
The field starts empty; the client does not derive suggestions. Errors are shown inline.

**Account settings**

- The user fills the change form (password, username, or email), presses Save, and a
  dialog asks for the code sent to their email. The code field uses
  `autocomplete="one-time-code"` and shows the resend countdown.
- When `hasPassword = false`, the password section is labelled **Set password**.
- Closing a dialog returns focus to the control that opened it.

All strings come from i18n in English and Vietnamese. Every form exposes pending,
success, validation, and API-error states.

## 12. Security and observability

- Logs and audit metadata redact OAuth codes, `state`, verifiers, `flowId`, email codes,
  reauth tickets, and tokens.
- Audit events: `google_sign_in`, `google_identity_linked`, `unverified_profile_reclaimed`,
  `username_claimed`, `password_reset_requested`, `password_reset`, `reauth_code_sent`,
  `reauth_verified`, `password_set`, `password_changed`, `username_changed`,
  `email_change_requested`. Failures are recorded with `succeeded = false`.
- Provider outages never fall back to local authentication.
- A Google-only account signing in with username + password gets the generic
  `INVALID_CREDENTIALS`.

## 13. Configuration prerequisites

Documented in `docs/AUTH_SETUP.md` and `auth.local.example.json`:

1. **Custom SMTP** in Supabase. The built-in email service sends only to project team
   members at a very low rate; real users would receive neither codes nor the existing
   sign-up verification email.
2. Email templates "Reset Password" and "Magic Link" show `{{ .Token }}`; email OTP length
   6, expiry 600 seconds.
3. A Google Cloud OAuth client (Web application) whose authorized redirect URI is
   `https://<project-ref>.supabase.co/auth/v1/callback`; its ID and secret go into the
   Supabase Google provider.
4. Supabase Redirect URLs contain the value of `SUPABASE_GOOGLE_REDIRECT_URL` followed by
   `**` (the Host appends `?state=…`, §6.5).
5. Supabase "Allow new users to sign up" stays on; QuestLog's `registrationEnabled` is
   enforced by the Host (§5 rule 2, §7).
6. Host variables `SUPABASE_GOOGLE_ENABLED=true` and `SUPABASE_GOOGLE_REDIRECT_URL`.

## 14. Verification

**SQL** — `supabase/tests/account_google_sql_test.ts` (PGlite, with a stubbed
`auth.users`, following `crawler_sql_test.ts`):

- Password sign-up creates a `complete` profile with `has_password = true`.
- Google sign-up without username creates `username_required`, `has_password = false`.
- Email provider without username still raises `USERNAME_REQUIRED`.
- `complete` without a username violates the constraint.
- Several `username_required` profiles coexist under the unique constraint.
- Backfill marks existing rows `complete`, `has_password = true`, and the right
  `signup_method`.

**Host** — new project `MasterPlay.Host.Tests` (xUnit, `WebApplicationFactory`, Supabase
faked through an `HttpMessageHandler`):

- PKCE S256 challenge, single-use `state` and `flowId`, flow expiry, store capacity.
- Web flow rejected without the matching cookie; Desktop flow rejected without the
  desktop token or from a non-loopback address.
- Code exchanged only at `complete`; a cancelled callback yields `OAUTH_CANCELLED`.
- §5 rules 1–4, including user deletion when registration is disabled and the reclaim of
  an unverified profile.
- `USERNAME_REQUIRED` gate and its allowlist; concurrent username claims.
- Forgot always returns 202 (unknown identifier, cooldown); reset signs in and revokes
  other sessions.
- Reauth: cooldown, 5-attempt limit, ticket single use, binding to account and session,
  expiry; `currentPassword` accepted only while `must_change_password = true`.
- Captured logs contain no code, state, verifier, flow ID, ticket, or token.

**Web** — `node --test`: a pure `googleAuthFlow.ts` module (polling, cancel, timeout,
retry, error-code mapping) and the email-code countdown helper.

**Manual end-to-end against a real Supabase project**

- Web: new Google user → popup → username → library.
- Desktop: system browser → return to WebView2; confirm `window.open` after an awaited
  request reaches the Shell's `OpenExternal`.
- Google email equal to an existing verified account → automatic link, same library.
- Registration disabled → new Google user rejected and deleted; existing user signs in.
- Locked account rejected.
- Forgot password by username and by email; Google-only account sets a first password,
  then signs in with username + password.
- Change password, username, and email with an email code.

`npm run typecheck && npm run lint && npm run test` and the backend build/tests pass.

## 15. Delivery slices

1. Migration, trigger, backfill, rollback, SQL tests.
2. Email codes: forgot/reset, reauth code/verify, ticket store, sensitive-endpoint
   changes, contract and regenerated types. Ships independently of Google and closes the
   missing forgot-password gap.
3. Google flow on the Host: flow store, start/callback/complete, §5 rules, onboarding gate,
   username claim, contract.
4. Web and Desktop UI for slices 2 and 3.
5. Documentation and manual end-to-end verification.

Documentation updated with the implementation: `docs/openapi.yaml`,
`docs/API_CONTRACT.md` §1.2 and §1.6, `docs/DOMAIN_RULES.md` (onboarding and
reauthentication rules), `docs/AUTH_SETUP.md`, `auth.local.example.json`, and row B9 of
`docs/PARITY_CHECKLIST.md`.

## 16. Out of scope

- Linking or unlinking Google from settings with a different email (requires Supabase
  manual linking, currently beta).
- Google as a reauthentication method.
- MFA. When MFA lands, Google sign-in passes through the same MFA gate as password login.
- A shared flow/ticket store for multi-instance Hosted deployments.
- Cleanup of abandoned `username_required` accounts.
- Other OAuth providers.

## 17. Rejected alternatives

- **PKCE in the frontend with supabase-js.** On Desktop the verifier would live in
  WebView2 while the callback lands in the system browser, forcing codes and refresh
  tokens to be relayed between browser, Host, and WebView2.
- **Custom `questlog://` URI scheme owned by the Shell.** Needs installer registration,
  single-instance hand-off, and a custom-scheme allowlist entry, with no benefit over the
  loopback Host.
- **Auto-generated usernames from the Google email.** Produces unwanted usernames and
  can expose the email's local part.
- **Rejecting automatic linking.** Supabase links verified emails by design; undoing it
  would require racing an identity removal after the fact.
- **Email links instead of codes.** Links to a loopback origin fail on other devices, and
  PKCE links fail outside the initiating browser.
- **Keeping `currentPassword` alongside email codes.** Two reauthentication branches in
  every form and endpoint, while forgot-password already makes email control equivalent
  to account control.
