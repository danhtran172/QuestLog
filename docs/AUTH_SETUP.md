# QuestLog account setup

QuestLog blocks hosted/web library access behind the account screen. Authentication is intentionally disabled until a Supabase project is connected; the application never falls back to an insecure local password store. The desktop app additionally supports an explicit guest session protected by a per-launch WebView2 handshake token; it accesses only local device data and is not a Supabase identity.

## Local desktop configuration

For the desktop app, copy [`auth.local.example.json`](../auth.local.example.json) to:

```text
%APPDATA%\QuestLog\auth.local.json
```

Fill in the Supabase URL, publishable key (`SUPABASE_PUBLISHABLE_KEY`; the legacy
`SUPABASE_ANON_KEY` name is also accepted), and service-role secret
(`SUPABASE_SECRET_KEY`; the legacy `SUPABASE_SERVICE_ROLE_KEY` name is also accepted),
and bootstrap administrator values. The Host reads this file at startup. The file is
ignored by Git; never commit it or paste its service-role value into frontend code.
Environment variables with the same names are still supported and take precedence.

## 1. Apply the database migration

Run [`supabase/migrations/202608240001_account_foundation.sql`](../supabase/migrations/202608240001_account_foundation.sql) in the Supabase SQL editor or through the Supabase CLI for the target project.

The migration creates:

- case-insensitive, unique QuestLog usernames;
- `admin` and `user` roles;
- active/locked account state;
- account audit storage;
- profile and audit RLS;
- the Auth user-to-profile trigger;
- the Custom Access Token Hook function.

After the migration, enable `public.custom_access_token_hook` under **Authentication → Hooks → Custom Access Token** in the Supabase dashboard.

## 2. Configure Supabase Auth

- Require email confirmation.
- Set the local site URL to `http://localhost:5173` during development.
- Add only the production QuestLog origins and callbacks when deploying Hosted Web.
- Keep the service-role key on the Host/API only. Never add it to a `VITE_*` variable or frontend file.

### 2.1 Email delivery (required for forgot-password and sensitive changes)

QuestLog sends **6-digit codes**, not links, for forgot-password and for confirming password,
username, and email changes. Desktop links would point at `localhost` and fail on a phone.

1. **Authentication → Emails → SMTP Settings**: configure a custom SMTP provider (Resend, Amazon SES,
   Gmail SMTP, …). Supabase's built-in sender only delivers to project team members at a very low
   rate, so real users would get neither codes nor the sign-up verification email.
2. **Authentication → Emails → Templates**:
   - **Reset Password**: show the code, e.g. `Your QuestLog reset code is {{ .Token }}`.
   - **Magic Link**: show the code, e.g. `Your QuestLog confirmation code is {{ .Token }}`.
     QuestLog uses this template only for reauthentication codes.
3. **Authentication → Providers → Email**: email OTP length `6`, expiry `600` seconds.

### 2.2 Google sign-up and sign-in

Apply migration `20261004150000_google_signup_onboarding.sql` before enabling Google. Without it the Host
falls back to the old profile columns: password sign-in keeps working, Google sign-up cannot.

1. In Google Cloud Console, create an OAuth client of type **Web application**. Its authorized
   redirect URI is `https://<project-ref>.supabase.co/auth/v1/callback`.
2. In Supabase **Authentication → Providers → Google**, enable the provider and paste that client ID
   and secret.
3. In Supabase **Authentication → URL Configuration → Redirect URLs**, add the QuestLog callback
   followed by `**` (the Host appends `?state=…`):
   - development: `http://localhost:5173/api/auth/google/callback**`
   - Hosted Web: `https://<questlog-origin>/api/auth/google/callback**`
4. Keep **Allow new users to sign up** on. QuestLog enforces `QUESTLOG_REGISTRATION_ENABLED` itself.
5. Set on the Host (or in `auth.local.json`):
   - `SUPABASE_GOOGLE_ENABLED=true`
   - `SUPABASE_GOOGLE_REDIRECT_URL=http://localhost:5173/api/auth/google/callback` (same URL as step 3,
     without `**`). `GET /api/auth/config` keeps `googleEnabled: false` until both are set.

New Google users pick a username before entering the library. A Google email that matches an existing
**verified** QuestLog account signs into that account (Supabase automatic identity linking).
The desktop app opens Google in the system browser and picks up the session when you return.

## 3. Start the Host with secrets

Set these variables in the terminal/process that starts `MasterPlay.Host`:

```powershell
$env:SUPABASE_URL = "https://<project-ref>.supabase.co"
$env:SUPABASE_PUBLISHABLE_KEY = "<publishable-key>"
$env:SUPABASE_SECRET_KEY = "<secret-key>"

$env:QUESTLOG_BOOTSTRAP_ADMIN_USERNAME = "<approved-admin-username>"
$env:QUESTLOG_BOOTSTRAP_ADMIN_EMAIL = "<approved-admin-email>"
$env:QUESTLOG_BOOTSTRAP_ADMIN_PASSWORD = "<bootstrap-password>"

.\run-web.cmd -b
```

The password variable is consumed only during startup and is removed from the Host child process afterward. Bootstrap is idempotent: restarting does not reset an existing account's password, role, or security state.

Do not commit a filled `.env`, PowerShell profile, `appsettings` file, or launch profile. Once the administrator has signed in and changed the bootstrap password, remove the bootstrap password from the parent terminal/environment too.

## 4. Verify

1. `GET /api/auth/config` returns `configured: true` without returning any key.
2. The QuestLog window shows Sign in and Create account.
3. Registering creates only a `user` profile and sends an email confirmation.
4. Sign in accepts either the username or email.
5. More → Account opens account settings with username, email, password, and Sign out actions.
6. Changing username asks for a 6-digit code sent by email and rejects a duplicate normalized username.
7. Changing email asks for a 6-digit code; the old email stays active until the confirmation flow completes.
8. Calling a domain endpoint without `Authorization: Bearer <token>` returns `401`.
9. Locking a profile blocks both a new login and an existing token on its next API request.
10. **Forgot password?** on the sign-in screen emails a code; entering it with a new password signs in.
11. With Google configured, **Continue with Google** creates an account, asks for a username, then opens the library.

Use the rollback SQL only against a disposable or explicitly approved environment. It removes account profile and audit tables and must not be run as a routine deployment step.
