# Account, Shared Catalog, and Private Library Design

## Goal

Add production account support to QuestLog with two roles, `admin` and `user`, while separating shared game metadata from private user and device data.

The system must support self-registration, verified email, login by username or email, password and username changes, Google identity linking, session management, and MFA. The bootstrap administrator must change the bootstrap password and enroll TOTP before entering the application.

This design applies to the QuestLog Web application, Desktop Shell, Local Host API, Hosted API, Supabase Auth, and the future Supabase PostgreSQL data store. It replaces the current single-user assumption; it does not grant administrators access to another user's library.

## Confirmed product decisions

- Roles are `admin` and `user`.
- Self-registration is enabled and always creates a `user` account.
- Email verification is required before normal app access.
- Login accepts either username or email plus password.
- Usernames are unique without regard to case.
- Google can be linked to an existing account.
- TOTP MFA is mandatory for administrators after their first password change and optional for users.
- The bootstrap administrator is created idempotently from deployment secrets. Its password is never stored in source control and must be changed on first login.
- In v1, administrators can list accounts, change roles, and lock or unlock accounts. They cannot permanently delete accounts or inspect another user's library.
- Shared catalog metadata is readable by authenticated users and writable only by administrators.
- A game created by a user outside the shared catalog remains private permanently. There is no review or promotion workflow.
- Library state is private to each user.
- Paths and launch configuration are private to both the user and the device.
- Platform passwords such as Steam and Ubisoft passwords remain in an encrypted local vault and are not synchronized.
- Existing QuestLog data is migrated to the bootstrap administrator. The current machine becomes that account's first device.

## Chosen architecture

Use Supabase Auth and Supabase PostgreSQL with Row Level Security, with the QuestLog .NET API as the only domain-data gateway.

- Supabase Auth owns password identities, verified emails, Google identities, sessions, refresh tokens, and TOTP factors.
- A Custom Access Token Auth Hook adds the QuestLog role claim to access tokens.
- The .NET API validates Supabase JWTs, derives the current user from `sub`, and applies domain authorization.
- PostgreSQL RLS independently enforces row ownership and shared-catalog rules.
- The frontend may use Supabase capabilities only through the account/auth boundary. It does not query or mutate domain tables directly.
- Local Host and Hosted API expose the same versioned OpenAPI contract and authorization semantics.

Supabase password sign-in natively accepts email or phone rather than username. Therefore QuestLog's auth gateway resolves a normalized username to the account's email internally before asking Supabase Auth to authenticate. It never returns the resolved email to the caller and never logs the submitted password.

Rejected alternatives:

- Supabase Auth with per-machine SQLite would not provide one shared catalog, cross-device user synchronization, or complete RLS isolation.
- Custom ASP.NET Identity would duplicate Google OAuth, email verification, MFA, session recovery, and other security work already provided by the selected platform.

## Data ownership model

### Account tables

`profiles`

- `user_id`, referencing the Supabase Auth user
- `username`
- `normalized_username`
- `role`: `admin` or `user`
- `status`: `active` or `locked`
- `must_change_password`
- `admin_mfa_required`
- display and audit timestamps

`normalized_username` has a unique database constraint. Normalization is defined once by the backend and used for registration, login, and username changes. The client does not implement uniqueness rules.

`account_audit_events`

- actor and subject IDs when applicable
- event type
- success/failure result
- safe device/session context
- timestamp
- redacted metadata only

Audit rows never contain passwords, tokens, MFA codes or seeds, platform secrets, or full sensitive paths.

### Unified game metadata table

`games` stores both catalog and private game metadata so that every library entry can use one stable foreign key.

- Shared catalog row: `owner_user_id` is null and `visibility = 'catalog'`.
- Private row: `owner_user_id` is required and `visibility = 'private'`.

Metadata fields migrated from the current `Game` entity include:

- `Name`, `SlugName`, and `IgdbId`
- `IsDlc` and `BaseGameId`
- `ImagePath`, `GImagePath`, and `SteamLibraryImagePath`
- `PublishDate`, `Publisher`, and `Developer`
- `SeriesId`, `MetaScore`, and `AvgPlayTime`
- genre, theme, and series relationships

Authenticated users may read catalog rows. Only administrators may create or edit catalog rows. Only the owner may read or edit a private row. Private games are not visible in search, admin catalog lists, logs, or aggregate responses belonging to another user.

### Private user library

`user_library_entries` is the user-to-game relation and is unique on `(user_id, game_id)`.

It owns the current user's:

- `Score`
- `Excited`
- `TotalPlayTimeSeconds`
- selected progress status
- selected ownership status
- library-added timestamp derived from `CreateAt`

Private child data references the library entry rather than the shared game directly:

- `game_finishes`: each completion date and score
- `play_diaries`: date, time spent, and notes
- `play_queue_positions`: the user's play order
- `ownership_info`: platform account names, support URL, download URL, and note

Progress and ownership status definitions are also account-scoped. A default set is copied or provisioned for every new account; users may customize their own labels, icons, colors, and sort order without changing another account.

### Device-private data

`devices`

- owner user ID
- stable installation ID
- user-editable device name
- platform and app version context
- created, last-seen, and revoked timestamps

`game_installations` is unique on `(device_id, user_library_entry_id)` and owns:

- `ExeName` and `InitExeName`
- launch target kind and path
- `Ready` and `IsDisabled`
- save folder and backup folder paths
- other native launch/scan state that is meaningful only on one machine

Detected process data is transient local runtime state. It is not synchronized as catalog or library data.

Revoking a device invalidates its account session and prevents further synchronization. It does not delete cloud progress, playtime, finish history, or ownership state.

### Local secret vault

Steam, Ubisoft, and future platform passwords are removed from cloud/domain DTOs. The Desktop Shell stores them with Windows Credential Manager or DPAPI, scoped to the local Windows user and QuestLog installation.

The cloud stores only a non-secret indication that a local credential may be configured for the current device. The API never returns platform passwords as part of a game detail response. A secret can be revealed or replaced only through a local, reauthenticated vault operation.

## Relationship overview

```text
auth.users
    └── profiles
        ├── devices
        ├── private games
        ├── progress status definitions
        ├── ownership status definitions
        └── user_library_entries
              ├── game (catalog or owned private game)
              ├── progress / ownership selection
              ├── game_finishes
              ├── play_diaries
              ├── play_queue_positions
              ├── ownership_info
              └── game_installations ── devices
```

## Authentication and account lifecycle

### Bootstrap administrator

An idempotent, explicitly invoked bootstrap operation reads these deployment secrets without providing committed defaults:

- `QUESTLOG_BOOTSTRAP_ADMIN_USERNAME`
- `QUESTLOG_BOOTSTRAP_ADMIN_EMAIL`
- `QUESTLOG_BOOTSTRAP_ADMIN_PASSWORD`

The deployment supplies the approved initial administrator identity. The operation creates the account only when absent, assigns `admin`, and sets `must_change_password = true` and `admin_mfa_required = true`. Re-running it does not reset an existing password, role, MFA factor, or profile.

The first successful login is restricted to this state machine:

```text
bootstrap password accepted
    -> password change required
    -> TOTP enrollment required
    -> TOTP verification required
    -> normal account access
```

No close, back, or deep-link path may bypass the gate.

### Registration

1. The user submits username, email, password, and CAPTCHA proof when required.
2. The backend reserves the normalized username and requests a Supabase signup.
3. A profile with role `user` is created transactionally/idempotently.
4. The account remains in email-verification state.
5. After verification, default progress and ownership catalogs are provisioned.
6. The first app session registers the current device.

Client input can never choose an account role.

### Login by username or email

The login endpoint accepts one `identifier` field.

- If it has email syntax, it is normalized and passed to the auth provider.
- Otherwise, the server looks up `normalized_username`, resolves the auth email internally, and authenticates with the submitted password.
- Unknown identifiers, wrong passwords, locked accounts, and unverified accounts return safe, non-enumerating messages at the public boundary. UI guidance for verified users may be provided only after identity is established safely.

The endpoint is rate limited by IP, account signal, and device context. Repeated failures require CAPTCHA and escalating backoff.

### Password, email, and username changes

- Password changes require the current password or a recent reauthentication and invalidate other sessions according to the user's choice.
- Forgotten passwords use Supabase's email reset flow and a strict allowlist of redirect URLs.
- Email changes require verification of the new address before it becomes the login identity.
- Username changes run entirely through the backend under the database uniqueness constraint. A collision returns a structured conflict error.
- Every successful or failed sensitive change emits a redacted audit event and appropriate security notification.

### Google identity linking

Google linking uses OAuth Authorization Code with PKCE and an allowlisted redirect.

- Hosted Web returns through the hosted auth callback.
- Desktop opens the system browser and returns through an approved deep link or loopback callback owned by the Shell.
- Linking and unlinking require recent reauthentication.
- QuestLog cannot unlink the last usable sign-in identity.
- Linking never creates a second QuestLog profile or library for the same authenticated user.

### MFA

- Administrators must maintain at least one verified TOTP factor.
- Users may enroll or remove TOTP voluntarily.
- Enrollment returns the QR/secret once and requires a valid challenge before becoming active.
- Recovery codes, if enabled by the provider implementation, are displayed once and stored only as hashes server-side.
- Removing or replacing a factor requires recent reauthentication and creates a security notification.

### Sessions

Access tokens are short lived and remain in memory. Refresh-session persistence is implemented behind an environment-specific session adapter:

- Desktop uses Windows Credential Manager/DPAPI.
- Hosted Web uses an auth gateway with `HttpOnly`, `Secure`, and appropriate `SameSite` cookies.

The account UI lists active devices/sessions and supports revoking one session or all other sessions. Locking an account revokes all sessions immediately and also changes the database account state so an already-issued access token cannot retain data access until expiry.

## Authorization and RLS

The backend always derives the user ID from the validated JWT subject. Domain mutation bodies and query strings never control ownership.

| Capability | User | Admin |
|---|---:|---:|
| Read shared catalog | Yes | Yes |
| Mutate shared catalog | No | Yes |
| CRUD a private game | Own only | Own only |
| Read or mutate library | Own only | Own only |
| Read device/install data | Own only | Own only |
| List account admin fields | No | Yes |
| Change role or lock state | No | Yes |
| Read another user's library | No | No |

RLS rules follow default deny:

- Catalog metadata allows authenticated reads and admin mutations.
- Private games require `auth.uid() = owner_user_id`.
- Library, finishes, diaries, queue, ownership information, status definitions, devices, and installations require `auth.uid() = user_id` through their ownership chain.
- Admin claims do not bypass private-library policies.
- Account status is checked from authoritative database state for sensitive requests, not solely from a possibly stale JWT role claim.

Role changes and lock/unlock operations are backend-only admin commands. The system prevents demoting, locking, or otherwise disabling the last active administrator.

## API surface

The exact request and response schemas will be added to `docs/openapi.yaml` before backend or frontend implementation. Intended resource groups are:

```text
/api/auth/*
  signup, login, verify, refresh, logout
  forgot-password, reset-password, change-password
  change-email, change-username
  google/link, google/unlink
  mfa/enroll, mfa/challenge, mfa/unenroll
  sessions, sessions/revoke

/api/account/*
  profile, devices, security-events

/api/catalog/games/*
  authenticated reads, admin mutations

/api/library/*
  entries, private games, progress, ownership
  finishes, diary, queue, ownership information

/api/devices/*
  registration, rename, revoke, installations

/api/admin/accounts/*
  list, role change, lock, unlock
```

All failures use the existing structured API error envelope. Generated TypeScript under `MasterPlay.Web/src/lib/api/**` remains read-only and is regenerated from OpenAPI.

## User experience

### Signed-out screens

- Sign in contains `Username or email`, password, Google, and Forgot password.
- Sign up contains username, email, password, password confirmation, and CAPTCHA when required.
- A pending-verification screen supports rate-limited resend.
- Public errors do not reveal whether an identifier exists.

### Account settings

- Profile: username, email, role, username/email changes.
- Security: password, TOTP, recovery, and sign out other sessions.
- Connections: link or unlink Google.
- Devices: current-device indication, last activity, rename, and revoke.
- Activity: the user's security event history.

Sensitive operations require recent password or MFA reauthentication.

### Library and editor behavior

- Add Game searches the shared catalog first and offers Create Private Game when needed.
- Catalog games show a Catalog badge; private games show a Private badge.
- Catalog metadata is read-only for users. Administrators receive explicit catalog editing actions.
- User score, excitement, playtime, progress, ownership, finishes, diary, and queue remain editable within the user's library.
- Launcher, save folder, and backup folder show only the current device's installation state.
- A new device retains synchronized cloud library data but starts without paths or launcher configuration.
- Platform password fields clearly state Stored on this device and do not appear populated on another device.

### Admin accounts surface

The admin-only Accounts screen supports searching accounts and viewing only necessary management information: username, email verification state, role, lock state, MFA enrollment state, and last activity.

Role changes and lock/unlock actions use confirmation dialogs. The UI has no route or action for another user's library and no permanent-delete action. Last-admin protections are enforced by the backend and reflected by disabled UI actions.

All new strings use i18n. Forms expose pending, success, validation, and API error states and meet keyboard and accessible-name requirements.

## Existing-data migration

### Field mapping

| Current field/model | New owner |
|---|---|
| Name, SlugName, IgdbId, DLC/base game, images, release metadata, series, genres, themes, MetaScore, AvgPlayTime | game metadata |
| Score, Excited, TotalPlayTimeSeconds, ProgressId, OwnershipId, CreateAt | user library entry |
| GameFinish | library finish rows |
| PlayDiary | library diary rows |
| GamePosition | user queue row |
| ExeName, InitExeName, launch target, Ready, IsDisabled, save/backup paths | current-device installation |
| Platform account names, URLs, note | user ownership information |
| SteamPassword, UbisoftPassword | current-device encrypted vault |
| DetectedProcess | transient device runtime state |

Current games with a valid `IgdbId` become shared catalog candidates. Current games without `IgdbId` become private games owned by the bootstrap administrator to prevent accidental publication. Existing IDs are preserved where practical so relationships and auditability remain intact.

`FinishedIn` is treated as a legacy compatibility field. Canonical completion history comes from `GameFinish`; migration preserves a legacy value for manual review rather than synthesizing duplicate finish rows when an equivalent finish already exists.

### Migration sequence

1. Create a read-only backup of SQLite and current configuration.
2. Run preflight validation for duplicate IDs, broken relationships, invalid status references, and ambiguous finish data.
3. Create PostgreSQL schema, constraints, RLS policies, Auth Hook, and default account catalogs.
4. Invoke secure administrator bootstrap.
5. Import shared metadata and private metadata.
6. Import all current library data under the bootstrap administrator.
7. Register the current machine and import its installation/path data.
8. Move existing platform passwords into the local DPAPI vault, read them back for verification, and only then remove their plaintext database copies.
9. Regenerate the API contract clients and enable account-aware frontend flows.
10. Require JWT authorization on legacy domain endpoints before cutover.

The migration is idempotent. Re-running it cannot duplicate the administrator, games, library entries, finishes, diary entries, or queue records. Each import unit is transactional. A failed migration leaves the SQLite backup usable and never marks a partial import as complete.

## Error handling and observability

- API errors distinguish validation, conflict, unauthenticated, forbidden, locked, rate-limited, and provider-unavailable conditions without enabling account enumeration.
- Authentication provider outages do not fall back to insecure local authentication.
- Mutations either complete atomically or return without partial domain state.
- Security logs redact credentials, tokens, TOTP material, OAuth codes, secret-vault values, and sensitive paths.
- Operational metrics track aggregate login failures, lockouts, verification delivery, MFA challenges, provider latency, and migration failures without leaking user content.
- Security notifications cover password/email changes, identity linking, MFA changes, new devices, session revocation, role changes, and account lock state.

## Verification

### Authentication and account tests

- Username normalization and case-insensitive uniqueness.
- Registration always creates role `user` and cannot accept a client role override.
- Email verification gate and resend cooldown.
- Login through both username and email with equivalent session results.
- Non-enumerating failure responses.
- Password reset/change, email change, and username conflict.
- Google link/unlink without duplicate profiles or libraries.
- Admin first-login password and TOTP state machine cannot be bypassed.
- Optional user TOTP and mandatory admin TOTP.
- Session listing, current-device detection, and targeted/all-other revocation.
- Lock immediately blocks new and existing sessions.
- Last active administrator cannot be demoted or locked.

### Authorization and data-isolation tests

- User A cannot select, infer, update, or delete any private row belonging to User B.
- An administrator cannot bypass another user's library RLS.
- Users can read but cannot mutate catalog metadata.
- Administrators can mutate catalog metadata.
- Private game search and detail remain invisible to non-owners.
- Device and installation rows require matching user ownership.
- Requests with a forged payload `user_id` remain scoped to the JWT subject or fail.

### Migration and secret tests

- Run migration against a disposable copy of representative SQLite data.
- Compare game, finish, diary, playtime, queue, status, and installation counts before and after.
- Verify no duplicate imports after a second run.
- Verify rollback on forced failures at each stage.
- Verify platform passwords enter the DPAPI vault and are absent from cloud/domain DTOs and logs.
- Verify paths, tokens, password values, OAuth codes, and TOTP secrets are redacted.

### Build and contract checks

- Update `docs/DOMAIN_RULES.md`, `docs/API_CONTRACT.md`, `docs/openapi.yaml`, and `docs/PARITY_CHECKLIST.md` as part of implementation.
- Generate frontend API types only through the repository generator.
- Run backend build and tests, migration integration tests, RLS tests, and desktop OAuth/vault tests.
- Run `npm run typecheck && npm run lint && npm run test` where those scripts exist; document and run the available equivalent when a package does not expose one.

## Delivery phases

1. Supabase project configuration, auth adapter, profiles, roles, bootstrap, and security policies.
2. Shared/private game schema, user library schema, device schema, RLS, and migration tooling.
3. Versioned auth, account, catalog, library, device, and admin API contracts.
4. Web/Desktop sign-in, registration, verification, first-admin gate, session storage, Google, and MFA.
5. Account settings, device management, private-game/catalog editor rules, and admin Accounts UI.
6. Existing-data migration, cutover, security review, full verification, and parity documentation.

Each phase must preserve the public contract established by the preceding phase and cannot be considered complete without its negative authorization tests.

## Out of scope for v1

- Administrator access to another user's game library or secrets.
- Permanent account deletion or self-service account deletion.
- Promotion or review of a user's private game into the shared catalog.
- Cloud synchronization of platform passwords.
- Social features, friends, shared libraries, household accounts, or library sharing.
- Additional roles or fine-grained catalog moderation roles.
