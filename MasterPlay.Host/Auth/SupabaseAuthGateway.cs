using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace MasterPlay.Host.Auth;

public sealed class SupabaseAuthGateway(
    IHttpClientFactory httpClientFactory,
    SupabaseAuthOptions options,
    EmailCodeThrottle codeThrottle,
    ILogger<SupabaseAuthGateway> logger,
    TimeProvider? timeProvider = null)
{
    public static readonly TimeSpan ValidationReuseWindow = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long a password sign-up that never confirmed its email keeps its
    /// username and email. After that the next sign-up for either releases it.
    /// </summary>
    public static readonly TimeSpan UnconfirmedSignupHold = TimeSpan.FromHours(6);
    private const int MaxValidatedTokens = 256;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly TimeProvider time = timeProvider ?? TimeProvider.System;
    private readonly ConcurrentDictionary<string, ValidatedToken> validatedTokens = new(StringComparer.Ordinal);
    private static readonly string[] OnboardingColumns = ["onboarding_state", "signup_method", "has_password"];
    private const string UndefinedColumn = "42703";

    // Set once PostgREST reports the onboarding columns missing (migration not applied yet).
    private volatile bool legacyProfileSchema;

    public async Task<(AuthSessionDto Session, string RefreshToken)> LoginAsync(
        string identifier,
        string password,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var email = identifier.Contains('@')
            ? identifier.Trim()
            : await ResolveUsernameEmailAsync(identifier, cancellationToken);

        using var request = CreateAnonRequest(
            HttpMethod.Post,
            "/auth/v1/token?grant_type=password");
        request.Content = JsonContent.Create(new { email, password });
        AuthTokenResponse token;
        try
        {
            token = await SendAsync<AuthTokenResponse>(request, cancellationToken);
        }
        catch (SupabaseAuthException exception) when (exception.Code == "email_not_confirmed")
        {
            // The client answers this by showing the signup-code step.
            throw EmailNotConfirmed();
        }

        var account = await GetAccountAsync(token.User.Id, token.User.Email, token.User.EmailConfirmedAt, cancellationToken);

        if (!string.Equals(account.Status, "active", StringComparison.Ordinal))
        {
            await LogoutAsync(token.AccessToken, cancellationToken);
            throw Locked();
        }

        return (ToSession(token, account), token.RefreshToken);
    }

    public async Task<SignupResponseDto> SignupAsync(
        string username,
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        if (!options.RegistrationEnabled)
        {
            throw RegistrationDisabled();
        }

        // Supabase answers a sign-up for an existing confirmed email with a fake
        // success (no error, no email), so QuestLog checks the email itself.
        if (await EmailExistsAsync(email, cancellationToken))
        {
            throw EmailTaken();
        }

        if (await UsernameExistsAsync(username, cancellationToken))
        {
            throw UsernameTaken();
        }

        using var request = CreateAnonRequest(HttpMethod.Post, "/auth/v1/signup");
        request.Content = JsonContent.Create(new
        {
            email,
            password,
            data = new { username = username.Trim() },
        });
        try
        {
            _ = await SendAsync<JsonElement>(request, cancellationToken);
        }
        catch (SupabaseAuthException exception) when (exception.StatusCode == 429)
        {
            throw EmailRateLimited();
        }

        // Supabase just sent the first code; resends wait out the usual cooldown.
        codeThrottle.TryRecordSend(SignupKey(email), out _);
        return new SignupResponseDto(true, email.Trim());
    }

    /// <summary>
    /// Re-sends the sign-up confirmation code. QuestLog has no web page for a
    /// confirmation link, so the "Confirm signup" email carries a 6-digit code.
    /// Unknown or already-confirmed identifiers are silent.
    /// </summary>
    public async Task<(bool Sent, DateTimeOffset ResendAvailableAt)> ResendSignupCodeAsync(
        string identifier,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var email = await TryResolveEmailAsync(identifier, cancellationToken);
        if (email is null)
        {
            return (true, time.GetUtcNow() + EmailCodeThrottle.ResendCooldown);
        }

        if (!codeThrottle.TryRecordSend(SignupKey(email), out var nextAllowedAt))
        {
            return (false, nextAllowedAt);
        }

        using var request = CreateAnonRequest(HttpMethod.Post, "/auth/v1/resend");
        request.Content = JsonContent.Create(new { type = "signup", email });
        using var response = await Client.SendAsync(request, cancellationToken);
        if ((int)response.StatusCode == 429)
        {
            throw EmailRateLimited();
        }

        if ((int)response.StatusCode >= 500)
        {
            throw ProviderFailure((int)response.StatusCode);
        }

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Supabase rejected a sign-up code resend with HTTP {Status}.", (int)response.StatusCode);
        }

        return (true, nextAllowedAt);
    }

    /// <summary>Confirms the account's email with the emailed code and signs the user in.</summary>
    public async Task<(AuthSessionDto Session, string RefreshToken)> VerifySignupCodeAsync(
        string identifier,
        string code,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var email = await TryResolveEmailAsync(identifier, cancellationToken);
        var throttleKey = email is null ? $"signup:unknown:{identifier.Trim().ToLowerInvariant()}" : SignupKey(email);
        if (codeThrottle.AttemptsExceeded(throttleKey))
        {
            throw AttemptsExceeded();
        }

        if (email is null)
        {
            codeThrottle.RecordFailure(throttleKey);
            throw CodeInvalid();
        }

        var token = await VerifyCodeAsync("signup", email, code, throttleKey, cancellationToken);
        var account = await GetAccountAsync(token.User.Id, token.User.Email, token.User.EmailConfirmedAt, cancellationToken);
        if (!string.Equals(account.Status, "active", StringComparison.Ordinal))
        {
            await LogoutAsync(token.AccessToken, cancellationToken, "local");
            throw Locked();
        }

        await WriteAuditAsync(token.User.Id, "email_verified", true, cancellationToken);
        return (ToSession(token, account), token.RefreshToken);
    }

    public async Task<(AuthSessionDto Session, string RefreshToken)> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        using var request = CreateAnonRequest(
            HttpMethod.Post,
            "/auth/v1/token?grant_type=refresh_token");
        request.Content = JsonContent.Create(new { refresh_token = refreshToken });
        var token = await SendAsync<AuthTokenResponse>(request, cancellationToken);
        var account = await GetAccountAsync(token.User.Id, token.User.Email, token.User.EmailConfirmedAt, cancellationToken);

        if (!string.Equals(account.Status, "active", StringComparison.Ordinal))
        {
            throw Locked();
        }

        return (ToSession(token, account), token.RefreshToken);
    }

    /// <summary>
    /// Runs before every authenticated API request. Asking Supabase costs two
    /// sequential round trips (user, then profile): 0.2 s warm, several seconds on
    /// a cold connection, and opening one game detail fires a burst of requests.
    /// A token proven valid moments ago is therefore reused for
    /// <see cref="ValidationReuseWindow"/>, and concurrent requests carrying the
    /// same token share a single provider check. The window bounds how long a
    /// session revoked elsewhere, or an account locked by an administrator, keeps
    /// working on this Host. Account changes made through this gateway apply at once.
    /// </summary>
    public async Task<AuthAccountDto> ValidateAccessTokenAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var key = TokenKey(accessToken);
        var now = time.GetUtcNow();
        var entry = validatedTokens.GetOrAdd(key, _ => NewValidation(accessToken, now));
        if (entry.ExpiresAt <= now)
        {
            var renewed = NewValidation(accessToken, now);
            entry = validatedTokens.TryUpdate(key, renewed, entry)
                ? renewed
                : validatedTokens.GetOrAdd(key, renewed);
        }

        if (validatedTokens.Count > MaxValidatedTokens) ForgetExpiredValidations(now);

        var validation = entry.Validation.Value;
        try
        {
            return await validation.WaitAsync(cancellationToken);
        }
        catch when (validation.IsFaulted || validation.IsCanceled)
        {
            // Only successes are reused: a rejected token or a provider outage is asked again.
            validatedTokens.TryRemove(KeyValuePair.Create(key, entry));
            throw;
        }
    }

    private ValidatedToken NewValidation(string accessToken, DateTimeOffset now) => new(
        // Shared by every request waiting on this token, so one caller hanging up
        // must not cancel it for the rest. The HttpClient timeout still bounds it.
        new Lazy<Task<AuthAccountDto>>(() => ValidateWithProviderAsync(accessToken, CancellationToken.None)),
        now + ValidationReuseWindow);

    private void ForgetExpiredValidations(DateTimeOffset now)
    {
        foreach (var (key, entry) in validatedTokens)
        {
            if (entry.ExpiresAt <= now) validatedTokens.TryRemove(KeyValuePair.Create(key, entry));
        }
    }

    /// <summary>Drops reused validations so the account's next request reads its profile again.</summary>
    private void ForgetValidatedAccount(string userId)
    {
        foreach (var (key, entry) in validatedTokens)
        {
            var validation = entry.Validation.IsValueCreated ? entry.Validation.Value : null;
            if (validation is null || !validation.IsCompletedSuccessfully || validation.Result.Id == userId)
            {
                validatedTokens.TryRemove(KeyValuePair.Create(key, entry));
            }
        }
    }

    private static string TokenKey(string accessToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(accessToken)));

    private async Task<AuthAccountDto> ValidateWithProviderAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = CreateAnonRequest(HttpMethod.Get, "/auth/v1/user");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        AuthUserResponse user;
        try
        {
            user = await SendAsync<AuthUserResponse>(request, cancellationToken);
        }
        catch (SupabaseAuthException exception) when (exception.StatusCode is 401 or 403)
        {
            // Supabase answers 403 session_not_found once the session is revoked (sign-out
            // elsewhere, password reset). Report 401 so the client refreshes or signs out.
            throw SessionExpired();
        }

        var account = await GetAccountAsync(user.Id, user.Email, user.EmailConfirmedAt, cancellationToken);

        if (!string.Equals(account.Status, "active", StringComparison.Ordinal))
        {
            throw Locked();
        }

        return account;
    }

    /// <param name="scope">
    /// Supabase logout scope: <c>null</c> (provider default, all sessions), <c>local</c>
    /// (this session only) or <c>others</c> (every session except this one).
    /// </param>
    public async Task LogoutAsync(string accessToken, CancellationToken cancellationToken, string? scope = null)
    {
        if (!options.IsConfigured || string.IsNullOrWhiteSpace(accessToken)) return;

        if (validatedTokens.TryRemove(TokenKey(accessToken), out var signedOut) &&
            scope != "local" &&
            signedOut.Validation is { IsValueCreated: true, Value.IsCompletedSuccessfully: true })
        {
            ForgetValidatedAccount(signedOut.Validation.Value.Result.Id);
        }

        var path = scope is null ? "/auth/v1/logout" : $"/auth/v1/logout?scope={scope}";
        using var request = CreateAnonRequest(HttpMethod.Post, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await Client.SendAsync(request, cancellationToken);
        // Logout is idempotent for QuestLog. An expired token must not prevent local cleanup.
    }

    /// <summary>
    /// Sets a new password. The caller has already proven the user's identity:
    /// either a reauth ticket, or <paramref name="currentPassword"/> during the
    /// bootstrap administrator's forced first change.
    /// </summary>
    public async Task<(AuthSessionDto Session, string RefreshToken)> ChangePasswordAsync(
        AuthAccountDto account,
        string newPassword,
        string? currentPassword,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        if (currentPassword is not null)
        {
            await VerifyPasswordAsync(account.Email, currentPassword, cancellationToken);
        }

        await SetPasswordAsync(account.Id, newPassword, cancellationToken);
        await PatchProfileAsync(
            account.Id,
            new { must_change_password = false, has_password = true },
            cancellationToken);
        await WriteAuditAsync(account.Id, account.HasPassword ? "password_changed" : "password_set", true, cancellationToken);

        return await LoginAsync(account.Email, newPassword, cancellationToken);
    }

    public async Task<AuthAccountDto> ChangeUsernameAsync(
        AuthAccountDto account,
        string username,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var trimmedUsername = username.Trim();
        if (await UsernameExistsAsync(trimmedUsername, cancellationToken, account.Id))
        {
            throw UsernameTaken();
        }

        using var profileRequest = CreateServiceRequest(
            HttpMethod.Patch,
            $"/rest/v1/profiles?user_id=eq.{Uri.EscapeDataString(account.Id)}");
        profileRequest.Headers.TryAddWithoutValidation("Prefer", "return=minimal");
        profileRequest.Content = JsonContent.Create(new
        {
            username = trimmedUsername,
            normalized_username = NormalizeUsername(trimmedUsername),
        });
        await SendNoContentAsync(
            profileRequest,
            cancellationToken,
            "USERNAME_TAKEN",
            "That username is already in use.");
        ForgetValidatedAccount(account.Id);
        await WriteAuditAsync(account.Id, "username_changed", true, cancellationToken);

        return account with { Username = trimmedUsername };
    }

    public async Task<EmailChangeResponseDto> ChangeEmailAsync(
        AuthAccountDto account,
        string accessToken,
        string email,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var trimmedEmail = email.Trim();
        if (await EmailExistsAsync(trimmedEmail, cancellationToken, account.Id))
        {
            throw new SupabaseAuthException(409, "EMAIL_TAKEN", "That email is already in use.");
        }

        using var updateRequest = CreateAnonRequest(HttpMethod.Put, "/auth/v1/user");
        updateRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        updateRequest.Content = JsonContent.Create(new { email = trimmedEmail });
        _ = await SendAsync<AuthUserResponse>(updateRequest, cancellationToken);
        await WriteAuditAsync(account.Id, "email_change_requested", true, cancellationToken);

        // Supabase keeps the current email active until its confirmation flow completes.
        // profiles.email is synchronized by GetAccountAsync only after /auth/v1/user reports the new email.
        return new EmailChangeResponseDto(true, trimmedEmail);
    }

    /// <summary>
    /// Gives a Google account its first username. The update is filtered on
    /// <c>onboarding_state</c>, so two concurrent claims cannot both succeed.
    /// </summary>
    public async Task<AuthAccountDto> ClaimUsernameAsync(
        AuthAccountDto account,
        string username,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        if (account.Onboarding != AccountOnboarding.UsernameRequired)
        {
            throw new SupabaseAuthException(409, "ONBOARDING_COMPLETE", "This account already has a username.");
        }

        if (!options.RegistrationEnabled)
        {
            throw RegistrationDisabled();
        }

        var trimmedUsername = username.Trim();
        if (await UsernameExistsAsync(trimmedUsername, cancellationToken, account.Id))
        {
            throw UsernameTaken();
        }

        using var request = CreateServiceRequest(
            HttpMethod.Patch,
            $"/rest/v1/profiles?user_id=eq.{Uri.EscapeDataString(account.Id)}&onboarding_state=eq.username_required");
        request.Headers.TryAddWithoutValidation("Prefer", "return=representation");
        request.Content = JsonContent.Create(new
        {
            username = trimmedUsername,
            normalized_username = NormalizeUsername(trimmedUsername),
            onboarding_state = "complete",
        });

        using var response = await Client.SendAsync(request, cancellationToken);
        if ((int)response.StatusCode == 409)
        {
            throw UsernameTaken();
        }

        if (!response.IsSuccessStatusCode)
        {
            throw ProviderFailure((int)response.StatusCode);
        }

        var updated = await response.Content.ReadFromJsonAsync<List<ProfileIdResponse>>(JsonOptions, cancellationToken);
        if (updated is not { Count: > 0 })
        {
            throw new SupabaseAuthException(409, "ONBOARDING_COMPLETE", "This account already has a username.");
        }

        ForgetValidatedAccount(account.Id);
        await WriteAuditAsync(account.Id, "username_claimed", true, cancellationToken);
        return account with { Username = trimmedUsername, Onboarding = AccountOnboarding.Complete };
    }

    public string BuildGoogleAuthorizeUrl(GoogleOAuthFlow flow)
    {
        EnsureConfigured();
        // Supabase does not echo a client state back, so QuestLog's state travels
        // inside redirect_to. The allowlist entry therefore ends in a wildcard.
        var redirectTo = $"{options.GoogleRedirectUrl}?state={Uri.EscapeDataString(flow.State)}";
        return $"{options.Url}/auth/v1/authorize" +
               "?provider=google" +
               $"&redirect_to={Uri.EscapeDataString(redirectTo)}" +
               $"&code_challenge={Uri.EscapeDataString(flow.CodeChallenge)}" +
               "&code_challenge_method=s256";
    }

    /// <summary>
    /// Exchanges a Google authorization code and applies the account lifecycle
    /// rules (design §5) before a session is returned.
    /// </summary>
    public async Task<(AuthSessionDto Session, string RefreshToken)> CompleteGoogleSignInAsync(
        string authorizationCode,
        string codeVerifier,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        using var request = CreateAnonRequest(HttpMethod.Post, "/auth/v1/token?grant_type=pkce");
        request.Content = JsonContent.Create(new { auth_code = authorizationCode, code_verifier = codeVerifier });
        var (token, providerNow) = await SendWithDateAsync<AuthTokenResponse>(request, cancellationToken);
        var user = token.User;
        var profile = await GetProfileAsync(user.Id, cancellationToken);
        var identities = user.Identities ?? [];
        var hasEmailIdentity = identities.Any(identity => identity.Provider == "email");

        // Rule 1: locked accounts never receive a session.
        if (!string.Equals(profile.Status, "active", StringComparison.Ordinal))
        {
            await LogoutAsync(token.AccessToken, cancellationToken, "local");
            throw Locked();
        }

        // Rule 2: Supabase removed an unconfirmed password identity while linking
        // Google. Whoever chose that username never proved control of the email.
        if (profile.SignupMethod == "password" && !hasEmailIdentity)
        {
            await SetPasswordAsync(user.Id, GoogleOAuthFlowStore.RandomToken(), cancellationToken);
            await PatchProfileAsync(
                user.Id,
                new
                {
                    username = (string?)null,
                    normalized_username = (string?)null,
                    onboarding_state = "username_required",
                    signup_method = "google",
                    has_password = false,
                    must_change_password = false,
                },
                cancellationToken);
            await WriteAuditAsync(user.Id, "unverified_profile_reclaimed", true, cancellationToken);
            profile = await GetProfileAsync(user.Id, cancellationToken);
        }

        // Rule 3: an unfinished Google registration cannot continue while
        // registration is closed.
        if (profile.OnboardingState == "username_required" && !options.RegistrationEnabled)
        {
            await LogoutAsync(token.AccessToken, cancellationToken, "local");
            await DeleteUserAsync(user.Id, cancellationToken);
            throw RegistrationDisabled();
        }

        // Rule 4: automatic linking to a verified account. Measured against the
        // provider clock, never the local one.
        var google = identities.FirstOrDefault(identity => identity.Provider == "google");
        if (hasEmailIdentity &&
            google?.CreatedAt is { } linkedAt &&
            linkedAt >= providerNow - GoogleOAuthFlowStore.Lifetime)
        {
            await WriteAuditAsync(user.Id, "google_identity_linked", true, cancellationToken);
        }

        await WriteAuditAsync(user.Id, "google_sign_in", true, cancellationToken);
        var account = await GetAccountAsync(user.Id, user.Email, user.EmailConfirmedAt, cancellationToken);
        return (ToSession(token, account), token.RefreshToken);
    }

    /// <summary>
    /// Sends a password-recovery code. Unknown identifiers and cooldowns are
    /// silent so the public response never reveals whether an account exists.
    /// </summary>
    public async Task SendRecoveryCodeAsync(string identifier, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var email = await TryResolveEmailAsync(identifier, cancellationToken);
        // Product decision (2026-10-04): tell the user when no account matches, rather
        // than pretending a code was sent. The "auth" rate limit slows enumeration.
        if (email is null) throw AccountNotFound();
        if (!codeThrottle.TryRecordSend(RecoveryKey(email), out _)) return;

        using var request = CreateAnonRequest(HttpMethod.Post, "/auth/v1/recover");
        request.Content = JsonContent.Create(new { email });
        using var response = await Client.SendAsync(request, cancellationToken);
        if ((int)response.StatusCode >= 500)
        {
            throw ProviderFailure((int)response.StatusCode);
        }

        await WriteAuditAsync(null, "password_reset_requested", response.IsSuccessStatusCode, cancellationToken, email);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Supabase rejected a password recovery request with HTTP {Status}.", (int)response.StatusCode);
        }

        // Supabase's email quota is project-wide (very low with its built-in sender).
        // Saying "code sent" while nothing was sent leaves the user waiting for an
        // email that never comes, so this one failure is reported. Trade-off: it is
        // only reachable for identifiers that exist, while the quota is exhausted.
        if ((int)response.StatusCode == 429)
        {
            throw EmailRateLimited();
        }
    }

    /// <summary>
    /// Checks a password-recovery code. Supabase consumes the code here, so the
    /// caller hands the returned account to <see cref="CompletePasswordResetAsync"/>
    /// through a short-lived reset ticket instead of asking for the code again.
    /// </summary>
    public async Task<(string UserId, string Email)> VerifyRecoveryCodeAsync(
        string identifier,
        string code,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var email = await TryResolveEmailAsync(identifier, cancellationToken);
        var throttleKey = email is null ? $"recover:unknown:{identifier.Trim().ToLowerInvariant()}" : RecoveryKey(email);
        if (codeThrottle.AttemptsExceeded(throttleKey))
        {
            throw AttemptsExceeded();
        }

        if (email is null)
        {
            codeThrottle.RecordFailure(throttleKey);
            throw CodeInvalid();
        }

        var token = await VerifyCodeAsync("recovery", email, code, throttleKey, cancellationToken);
        // The recovery session is not needed: the password is set through the admin API.
        await LogoutAsync(token.AccessToken, cancellationToken, "local");
        var profile = await GetProfileAsync(token.User.Id, cancellationToken);
        if (!string.Equals(profile.Status, "active", StringComparison.Ordinal))
        {
            throw Locked();
        }

        await WriteAuditAsync(token.User.Id, "password_reset_code_verified", true, cancellationToken);
        return (token.User.Id, token.User.Email);
    }

    /// <summary>Sets the new password for an account whose recovery code was just verified.</summary>
    public async Task<(AuthSessionDto Session, string RefreshToken)> CompletePasswordResetAsync(
        string userId,
        string email,
        string newPassword,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var profile = await GetProfileAsync(userId, cancellationToken);
        if (!string.Equals(profile.Status, "active", StringComparison.Ordinal))
        {
            throw Locked();
        }

        await SetPasswordAsync(userId, newPassword, cancellationToken);
        await PatchProfileAsync(
            userId,
            new { has_password = true, must_change_password = false },
            cancellationToken);
        await WriteAuditAsync(userId, "password_reset", true, cancellationToken);

        var session = await LoginAsync(email, newPassword, cancellationToken);
        // A forgotten password may mean a compromised one: keep only the new session.
        await LogoutAsync(session.Session.AccessToken, cancellationToken, "others");
        return session;
    }

    /// <summary>Sends a reauthentication code to the signed-in account's email.</summary>
    public async Task<(bool Sent, DateTimeOffset ResendAvailableAt)> SendReauthCodeAsync(
        AuthAccountDto account,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        if (!codeThrottle.TryRecordSend(ReauthKey(account.Id), out var nextAllowedAt))
        {
            return (false, nextAllowedAt);
        }

        using var request = CreateAnonRequest(HttpMethod.Post, "/auth/v1/otp");
        request.Content = JsonContent.Create(new { email = account.Email, create_user = false });
        try
        {
            _ = await SendAsync<JsonElement>(request, cancellationToken);
        }
        catch (SupabaseAuthException exception) when (exception.StatusCode == 429)
        {
            throw EmailRateLimited();
        }
        await WriteAuditAsync(account.Id, "reauth_code_sent", true, cancellationToken);
        return (true, nextAllowedAt);
    }

    public async Task VerifyReauthCodeAsync(
        AuthAccountDto account,
        string code,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var throttleKey = ReauthKey(account.Id);
        if (codeThrottle.AttemptsExceeded(throttleKey))
        {
            throw AttemptsExceeded();
        }

        var token = await VerifyCodeAsync("email", account.Email, code, throttleKey, cancellationToken);
        // Verification signs the user in again. Keep only the session they already had.
        await LogoutAsync(token.AccessToken, cancellationToken, "local");
        if (!string.Equals(token.User.Id, account.Id, StringComparison.Ordinal))
        {
            throw new SupabaseAuthException(401, "REAUTH_INVALID", "The code belongs to a different account.");
        }

        await WriteAuditAsync(account.Id, "reauth_verified", true, cancellationToken);
    }

    public async Task<bool> BootstrapAdminAsync(
        string username,
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        if (await UsernameExistsAsync(username, cancellationToken) ||
            await EmailExistsAsync(email, cancellationToken))
        {
            return false;
        }

        using var createRequest = CreateServiceRequest(HttpMethod.Post, "/auth/v1/admin/users");
        createRequest.Content = JsonContent.Create(new
        {
            email = email.Trim(),
            password,
            email_confirm = true,
            user_metadata = new { username = username.Trim() },
        });
        var user = await SendAsync<AuthUserResponse>(createRequest, cancellationToken);

        await PatchProfileAsync(
            user.Id,
            new
            {
                role = "admin",
                must_change_password = true,
                admin_mfa_required = true,
                signup_method = "bootstrap",
            },
            cancellationToken);
        return true;
    }

    /// <summary>
    /// Reads the <c>session_id</c> claim of an access token that Supabase has
    /// already validated. Falls back to a hash of the token when the claim is absent.
    /// </summary>
    public static string SessionIdFromAccessToken(string accessToken)
    {
        var parts = accessToken.Split('.');
        if (parts.Length == 3)
        {
            try
            {
                var payload = parts[1].Replace('-', '+').Replace('_', '/');
                payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
                using var document = JsonDocument.Parse(Convert.FromBase64String(payload));
                if (document.RootElement.TryGetProperty("session_id", out var sessionId) &&
                    sessionId.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(sessionId.GetString()))
                {
                    return sessionId.GetString()!;
                }
            }
            catch (Exception exception) when (exception is FormatException or JsonException)
            {
                // Not a JWT payload; use the token hash below.
            }
        }

        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(accessToken)));
    }

    private async Task<AuthTokenResponse> VerifyCodeAsync(
        string type,
        string email,
        string code,
        string throttleKey,
        CancellationToken cancellationToken)
    {
        using var request = CreateAnonRequest(HttpMethod.Post, "/auth/v1/verify");
        request.Content = JsonContent.Create(new { type, email, token = code.Trim() });
        using var response = await Client.SendAsync(request, cancellationToken);
        if ((int)response.StatusCode >= 500)
        {
            throw ProviderFailure((int)response.StatusCode);
        }

        if (!response.IsSuccessStatusCode)
        {
            codeThrottle.RecordFailure(throttleKey);
            throw CodeInvalid();
        }

        codeThrottle.RecordSuccess(throttleKey);
        return await response.Content.ReadFromJsonAsync<AuthTokenResponse>(JsonOptions, cancellationToken)
               ?? throw new SupabaseAuthException(
                   502,
                   "AUTH_PROVIDER_INVALID_RESPONSE",
                   "The authentication provider returned an empty response.");
    }

    private async Task SetPasswordAsync(string userId, string password, CancellationToken cancellationToken)
    {
        // The admin API sets the password regardless of Supabase's "secure
        // password change" window; QuestLog has already reauthenticated the user.
        using var request = CreateServiceRequest(
            HttpMethod.Put,
            $"/auth/v1/admin/users/{Uri.EscapeDataString(userId)}");
        request.Content = JsonContent.Create(new { password });
        _ = await SendAsync<AuthUserResponse>(request, cancellationToken);
    }

    private async Task DeleteUserAsync(string userId, CancellationToken cancellationToken)
    {
        using var request = CreateServiceRequest(
            HttpMethod.Delete,
            $"/auth/v1/admin/users/{Uri.EscapeDataString(userId)}");
        await SendNoContentAsync(request, cancellationToken);
    }

    private async Task PatchProfileAsync(string userId, object values, CancellationToken cancellationToken)
    {
        var body = JsonSerializer.SerializeToNode(values, JsonOptions)!.AsObject();
        if (legacyProfileSchema)
        {
            // Before migration 20261004150000 these columns do not exist yet.
            foreach (var column in OnboardingColumns) body.Remove(column);
            if (body.Count == 0) return;
        }

        using var request = CreateServiceRequest(
            HttpMethod.Patch,
            $"/rest/v1/profiles?user_id=eq.{Uri.EscapeDataString(userId)}");
        request.Headers.TryAddWithoutValidation("Prefer", "return=minimal");
        request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        await SendNoContentAsync(request, cancellationToken);
        ForgetValidatedAccount(userId);
    }

    /// <summary>
    /// Records an account audit event. A failed audit write is logged but does
    /// not undo an authentication step that has already happened at the provider.
    /// </summary>
    private async Task WriteAuditAsync(
        string? subjectUserId,
        string eventType,
        bool succeeded,
        CancellationToken cancellationToken,
        string? subjectEmail = null)
    {
        try
        {
            if (subjectUserId is null && subjectEmail is not null)
            {
                subjectUserId = await TryGetUserIdByEmailAsync(subjectEmail, cancellationToken);
            }

            using var request = CreateServiceRequest(HttpMethod.Post, "/rest/v1/account_audit_events");
            request.Headers.TryAddWithoutValidation("Prefer", "return=minimal");
            request.Content = JsonContent.Create(new
            {
                actor_user_id = subjectUserId,
                subject_user_id = subjectUserId,
                event_type = eventType,
                succeeded,
            });
            using var response = await Client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Account audit event {EventType} was not recorded (HTTP {Status}).",
                    eventType,
                    (int)response.StatusCode);
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or SupabaseAuthException)
        {
            logger.LogWarning(exception, "Account audit event {EventType} was not recorded.", eventType);
        }
    }

    private async Task<string?> TryGetUserIdByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var query = $"/rest/v1/profiles?select=user_id&email=eq.{Uri.EscapeDataString(email.Trim())}&limit=1";
        using var request = CreateServiceRequest(HttpMethod.Get, query);
        var profiles = await SendAsync<List<ProfileIdResponse>>(request, cancellationToken);
        return profiles.SingleOrDefault()?.UserId;
    }

    private async Task<string?> TryResolveEmailAsync(string identifier, CancellationToken cancellationToken)
    {
        var trimmed = identifier.Trim();
        if (trimmed.Contains('@'))
        {
            return await TryGetUserIdByEmailAsync(trimmed, cancellationToken) is null ? null : trimmed;
        }

        var normalized = NormalizeUsername(trimmed);
        var query = $"/rest/v1/profiles?select=email&normalized_username=eq.{Uri.EscapeDataString(normalized)}&limit=1";
        using var request = CreateServiceRequest(HttpMethod.Get, query);
        var profiles = await SendAsync<List<ProfileEmailResponse>>(request, cancellationToken);
        var email = profiles.SingleOrDefault()?.Email;
        return string.IsNullOrWhiteSpace(email) ? null : email;
    }

    private async Task<string> ResolveUsernameEmailAsync(
        string username,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeUsername(username);
        var query = $"/rest/v1/profiles?select=email&normalized_username=eq.{Uri.EscapeDataString(normalized)}&limit=1";
        using var request = CreateServiceRequest(HttpMethod.Get, query);
        var profiles = await SendAsync<List<ProfileEmailResponse>>(request, cancellationToken);
        var email = profiles.SingleOrDefault()?.Email;
        if (string.IsNullOrWhiteSpace(email))
        {
            throw InvalidCredentials();
        }

        return email;
    }

    /// <summary>
    /// True when another account holds the value matched by <paramref name="filter"/>.
    /// Used by sign-up, username/email changes and the Google username claim, so a
    /// sign-up that expired unconfirmed is deleted here instead of blocking anyone.
    /// </summary>
    private async Task<bool> IsHeldAsync(string filter, string? excludedUserId, CancellationToken cancellationToken)
    {
        using var request = CreateServiceRequest(HttpMethod.Get, $"/rest/v1/profiles?select=user_id&{filter}&limit=1");
        var profiles = await SendAsync<List<ProfileIdResponse>>(request, cancellationToken);
        var holder = profiles.FirstOrDefault(
            profile => !string.Equals(profile.UserId, excludedUserId, StringComparison.Ordinal));
        return holder is not null && !await TryReleaseExpiredSignupAsync(holder.UserId, cancellationToken);
    }

    private async Task<bool> TryReleaseExpiredSignupAsync(string userId, CancellationToken cancellationToken)
    {
        using var request = CreateServiceRequest(HttpMethod.Get, $"/auth/v1/admin/users/{Uri.EscapeDataString(userId)}");
        var user = await SendAsync<AuthUserResponse>(request, cancellationToken);
        // Only a plain password sign-up that never proved its email: no Google
        // identity, no bootstrap admin, older than the hold.
        if (user.EmailConfirmedAt is not null ||
            user.CreatedAt is not { } createdAt ||
            createdAt > time.GetUtcNow() - UnconfirmedSignupHold ||
            user.Identities?.Any(identity => identity.Provider != "email") == true ||
            (await GetProfileAsync(userId, cancellationToken)).SignupMethod != "password")
        {
            return false;
        }

        // The profile and its audit links go with the user (on delete cascade / set null).
        await DeleteUserAsync(userId, cancellationToken);
        await WriteAuditAsync(null, "unconfirmed_signup_released", true, cancellationToken);
        return true;
    }

    private Task<bool> UsernameExistsAsync(
        string username,
        CancellationToken cancellationToken,
        string? excludedUserId = null) =>
        IsHeldAsync(
            $"normalized_username=eq.{Uri.EscapeDataString(NormalizeUsername(username))}",
            excludedUserId,
            cancellationToken);

    private Task<bool> EmailExistsAsync(
        string email,
        CancellationToken cancellationToken,
        string? excludedUserId = null) =>
        IsHeldAsync($"email=eq.{Uri.EscapeDataString(email.Trim())}", excludedUserId, cancellationToken);

    private async Task<ProfileResponse> GetProfileAsync(string userId, CancellationToken cancellationToken)
    {
        const string baseColumns = "user_id,username,email,role,status,must_change_password,admin_mfa_required";
        var filter = $"&user_id=eq.{Uri.EscapeDataString(userId)}&limit=1";
        List<ProfileResponse> profiles;
        if (!legacyProfileSchema)
        {
            try
            {
                using var request = CreateServiceRequest(
                    HttpMethod.Get,
                    $"/rest/v1/profiles?select={baseColumns},{string.Join(',', OnboardingColumns)}{filter}");
                profiles = await SendAsync<List<ProfileResponse>>(request, cancellationToken);
                return profiles.SingleOrDefault() ?? throw ProfileMissing();
            }
            catch (SupabaseAuthException exception) when (exception.Code == UndefinedColumn)
            {
                // The Host was deployed before the onboarding migration. Keep password
                // sign-in working; Google stays unusable until the migration runs.
                legacyProfileSchema = true;
                logger.LogWarning(
                    "profiles has no onboarding columns. Apply supabase/migrations/20261004150000_google_signup_onboarding.sql.");
            }
        }

        using var legacyRequest = CreateServiceRequest(HttpMethod.Get, $"/rest/v1/profiles?select={baseColumns}{filter}");
        profiles = await SendAsync<List<ProfileResponse>>(legacyRequest, cancellationToken);
        var legacy = profiles.SingleOrDefault() ?? throw ProfileMissing();
        return legacy with { OnboardingState = "complete", SignupMethod = "password", HasPassword = true };
    }

    private static SupabaseAuthException ProfileMissing() =>
        new(
            403,
            "PROFILE_MISSING",
            "The account profile is not ready. Please try again.");

    private async Task<AuthAccountDto> GetAccountAsync(
        string userId,
        string email,
        DateTimeOffset? emailConfirmedAt,
        CancellationToken cancellationToken)
    {
        var profile = await GetProfileAsync(userId, cancellationToken);

        var effectiveEmail = email.Trim();
        if (!string.Equals(profile.Email, effectiveEmail, StringComparison.OrdinalIgnoreCase))
        {
            await PatchProfileAsync(userId, new { email = effectiveEmail }, cancellationToken);
        }

        return new AuthAccountDto(
            profile.UserId,
            profile.Username,
            effectiveEmail,
            profile.Role,
            profile.Status,
            emailConfirmedAt.HasValue,
            profile.MustChangePassword,
            profile.AdminMfaRequired,
            profile.OnboardingState == "username_required"
                ? AccountOnboarding.UsernameRequired
                : AccountOnboarding.Complete,
            profile.HasPassword);
    }

    private HttpRequestMessage CreateAnonRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, options.Url + path);
        request.Headers.TryAddWithoutValidation("apikey", options.AnonKey);
        return request;
    }

    private HttpRequestMessage CreateServiceRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, options.Url + path);
        request.Headers.TryAddWithoutValidation("apikey", options.ServiceRoleKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ServiceRoleKey);
        return request;
    }

    private async Task<T> SendAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken) =>
        (await SendWithDateAsync<T>(request, cancellationToken)).Result;

    /// <summary>Like <see cref="SendAsync{T}"/>, also returning the provider's <c>Date</c> header.</summary>
    private async Task<(T Result, DateTimeOffset ProviderNow)> SendWithDateAsync<T>(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        using var response = await Client.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
            return (
                result ?? throw new SupabaseAuthException(
                    502,
                    "AUTH_PROVIDER_INVALID_RESPONSE",
                    "The authentication provider returned an empty response."),
                response.Headers.Date ?? DateTimeOffset.UtcNow);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        string? providerCode = null;
        string? providerMessage = null;
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            providerCode = TryString(root, "code") ?? TryString(root, "error_code");
            providerMessage = TryString(root, "msg") ??
                              TryString(root, "message") ??
                              TryString(root, "error_description");
        }
        catch (JsonException)
        {
            // Provider HTML/plain-text failures are mapped to a safe generic error below.
        }

        if ((int)response.StatusCode is 400 or 401 &&
            (providerMessage?.Contains("credential", StringComparison.OrdinalIgnoreCase) == true ||
             providerMessage?.Contains("password", StringComparison.OrdinalIgnoreCase) == true))
        {
            throw InvalidCredentials();
        }

        LogProviderFailure(request, (int)response.StatusCode, providerCode);
        throw new SupabaseAuthException(
            (int)response.StatusCode,
            providerCode ?? "AUTH_PROVIDER_ERROR",
            WithReference(SafeProviderMessage((int)response.StatusCode, providerMessage), (int)response.StatusCode, providerCode));
    }

    // A generic message alone cannot be diagnosed from a screenshot; the HTTP status and
    // provider error code (e.g. a Postgres code) identify the failure without leaking data.
    private static string WithReference(string message, int status, string? providerCode) =>
        message.StartsWith("The authentication request could not be completed", StringComparison.Ordinal) ||
        message.StartsWith("Authentication is temporarily unavailable", StringComparison.Ordinal)
            ? $"{message.TrimEnd('.')} (HTTP {status}{(providerCode is null ? "" : $", {providerCode}")})."
            : message;

    // Path only: the query string can carry a username or email.
    private void LogProviderFailure(HttpRequestMessage request, int status, string? providerCode) =>
        logger.LogWarning(
            "Supabase {Method} {Path} failed with HTTP {Status} ({ProviderCode}).",
            request.Method,
            request.RequestUri?.AbsolutePath,
            status,
            providerCode ?? "no code");

    private async Task SendNoContentAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken,
        string? conflictCode = null,
        string? conflictMessage = null)
    {
        using var response = await Client.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode) return;

        if ((int)response.StatusCode == 409 && conflictCode is not null)
        {
            throw new SupabaseAuthException(409, conflictCode, conflictMessage ?? "The value is already in use.");
        }

        LogProviderFailure(request, (int)response.StatusCode, null);
        throw ProviderFailure((int)response.StatusCode);
    }

    private static string? TryString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string SafeProviderMessage(int statusCode, string? providerMessage)
    {
        if (statusCode == 429) return "Too many attempts. Please wait and try again.";
        if (providerMessage?.Contains("already", StringComparison.OrdinalIgnoreCase) == true)
            return "An account already uses that email or username.";
        if (providerMessage?.Contains("email", StringComparison.OrdinalIgnoreCase) == true &&
            providerMessage.Contains("confirm", StringComparison.OrdinalIgnoreCase))
            return "Verify your email before signing in.";
        return statusCode >= 500
            ? "Authentication is temporarily unavailable."
            : "The authentication request could not be completed.";
    }

    private static AuthSessionDto ToSession(AuthTokenResponse token, AuthAccountDto account) =>
        new(
            account,
            token.AccessToken,
            DateTimeOffset.UtcNow.AddSeconds(Math.Max(30, token.ExpiresIn)));

    private static SupabaseAuthException InvalidCredentials() =>
        new(401, "INVALID_CREDENTIALS", "Username/email or password is incorrect.");

    private static SupabaseAuthException SessionExpired() =>
        new(401, "SESSION_EXPIRED", "Your session has ended. Sign in again.");

    private static SupabaseAuthException Locked() =>
        new(423, "ACCOUNT_LOCKED", "This account is locked.");

    private static SupabaseAuthException RegistrationDisabled() =>
        new(403, "REGISTRATION_DISABLED", "Registration is disabled.");

    private static SupabaseAuthException UsernameTaken() =>
        new(409, "USERNAME_TAKEN", "That username is already in use.");

    private static SupabaseAuthException CodeInvalid() =>
        new(400, "CODE_INVALID", "The code is wrong or has expired.");

    private static SupabaseAuthException EmailTaken() =>
        new(409, "EMAIL_TAKEN", "That email already has an account.");

    private static SupabaseAuthException AccountNotFound() =>
        new(404, "ACCOUNT_NOT_FOUND", "No account matches that username or email.");

    private static SupabaseAuthException EmailRateLimited() =>
        new(429, "EMAIL_RATE_LIMITED", "Too many emails were sent recently. Try again later.");

    private static SupabaseAuthException EmailNotConfirmed() =>
        new(403, "EMAIL_NOT_CONFIRMED", "Verify your email before signing in.");

    private static SupabaseAuthException AttemptsExceeded() =>
        new(429, "CODE_ATTEMPTS_EXCEEDED", "Too many wrong codes. Request a new code.");

    private static SupabaseAuthException ProviderFailure(int statusCode) =>
        new(
            statusCode,
            "AUTH_PROVIDER_ERROR",
            statusCode >= 500
                ? "Authentication is temporarily unavailable."
                : "The authentication request could not be completed.");

    private async Task VerifyPasswordAsync(
        string email,
        string currentPassword,
        CancellationToken cancellationToken)
    {
        using var verifyRequest = CreateAnonRequest(
            HttpMethod.Post,
            "/auth/v1/token?grant_type=password");
        verifyRequest.Content = JsonContent.Create(new
        {
            email,
            password = currentPassword,
        });
        _ = await SendAsync<AuthTokenResponse>(verifyRequest, cancellationToken);
    }

    private static string NormalizeUsername(string username) => username.Trim().ToLowerInvariant();

    private static string RecoveryKey(string email) => $"recover:{email.Trim().ToLowerInvariant()}";

    private static string ReauthKey(string userId) => $"reauth:{userId}";

    private static string SignupKey(string email) => $"signup:{email.Trim().ToLowerInvariant()}";

    private void EnsureConfigured()
    {
        if (!options.IsConfigured)
        {
            throw new SupabaseAuthException(
                503,
                "AUTH_NOT_CONFIGURED",
                "QuestLog authentication is not configured on this server.");
        }
    }

    private HttpClient Client => httpClientFactory.CreateClient(nameof(SupabaseAuthGateway));

    private sealed record ValidatedToken(Lazy<Task<AuthAccountDto>> Validation, DateTimeOffset ExpiresAt);

    private sealed record AuthTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string RefreshToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("user")] AuthUserResponse User);

    private sealed record AuthUserResponse(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("email_confirmed_at")] DateTimeOffset? EmailConfirmedAt,
        [property: JsonPropertyName("identities")] List<AuthIdentityResponse>? Identities = null,
        [property: JsonPropertyName("created_at")] DateTimeOffset? CreatedAt = null);

    private sealed record AuthIdentityResponse(
        [property: JsonPropertyName("provider")] string Provider,
        [property: JsonPropertyName("created_at")] DateTimeOffset? CreatedAt);

    private sealed record ProfileIdResponse(
        [property: JsonPropertyName("user_id")] string UserId);

    private sealed record ProfileEmailResponse(
        [property: JsonPropertyName("email")] string Email);

    private sealed record ProfileResponse(
        [property: JsonPropertyName("user_id")] string UserId,
        [property: JsonPropertyName("username")] string? Username,
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("must_change_password")] bool MustChangePassword,
        [property: JsonPropertyName("admin_mfa_required")] bool AdminMfaRequired,
        [property: JsonPropertyName("onboarding_state")] string OnboardingState,
        [property: JsonPropertyName("signup_method")] string SignupMethod,
        [property: JsonPropertyName("has_password")] bool HasPassword);
}
