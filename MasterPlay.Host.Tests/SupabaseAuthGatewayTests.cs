using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MasterPlay.Host.Auth;
using Microsoft.Extensions.Logging.Abstractions;

namespace MasterPlay.Host.Tests;

/// <summary>
/// Minimal stand-in for the Supabase Auth and PostgREST endpoints the gateway calls.
/// </summary>
internal sealed class FakeSupabase : HttpMessageHandler
{
    public const string UserId = "11111111-1111-1111-1111-111111111111";
    public static readonly DateTimeOffset ProviderNow = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    public List<(HttpMethod Method, string PathAndQuery, string Body)> Requests { get; } = [];
    public JsonObject Profile { get; set; } = NewProfile();
    public JsonArray Identities { get; set; } = [Identity("google", ProviderNow)];
    public HttpStatusCode VerifyStatus { get; set; } = HttpStatusCode.OK;

    /// <summary>When set, <c>GET /auth/v1/user</c> answers like a revoked session.</summary>
    public bool SessionRevoked { get; set; }

    /// <summary>Simulates a database where the onboarding migration has not run.</summary>
    public bool LegacySchema { get; set; }

    public HttpStatusCode RecoverStatus { get; set; } = HttpStatusCode.OK;

    public static JsonObject NewProfile() => new()
    {
        ["user_id"] = UserId,
        ["username"] = null,
        ["email"] = "player@example.test",
        ["role"] = "user",
        ["status"] = "active",
        ["must_change_password"] = false,
        ["admin_mfa_required"] = false,
        ["onboarding_state"] = "username_required",
        ["signup_method"] = "google",
        ["has_password"] = false,
    };

    public static JsonObject Identity(string provider, DateTimeOffset createdAt) => new()
    {
        ["provider"] = provider,
        ["created_at"] = createdAt.ToString("O"),
    };

    public IEnumerable<string> Paths => Requests.Select(request => $"{request.Method} {request.PathAndQuery}");

    public IEnumerable<string> AuditEvents => Requests
        .Where(request => request.PathAndQuery.StartsWith("/rest/v1/account_audit_events", StringComparison.Ordinal))
        .Select(request => JsonNode.Parse(request.Body)!["event_type"]!.GetValue<string>());

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        var path = request.RequestUri!.PathAndQuery;
        Requests.Add((request.Method, path, body));

        if (path.StartsWith("/auth/v1/token", StringComparison.Ordinal) ||
            (path == "/auth/v1/verify" && VerifyStatus == HttpStatusCode.OK))
        {
            return Json(Token());
        }

        if (path == "/auth/v1/user" && SessionRevoked)
        {
            return Json(
                new JsonObject { ["code"] = "session_not_found", ["message"] = "Session from session_id claim in JWT does not exist" },
                HttpStatusCode.Forbidden);
        }

        if (path == "/auth/v1/verify") return Json(new JsonObject { ["msg"] = "Token has expired or is invalid" }, VerifyStatus);

        if (path == "/auth/v1/recover" && RecoverStatus != HttpStatusCode.OK)
        {
            return Json(new JsonObject { ["error_code"] = "over_email_send_rate_limit", ["msg"] = "email rate limit exceeded" }, RecoverStatus);
        }

        if (LegacySchema && (path.Contains("onboarding_state") || body.Contains("has_password") ||
                             body.Contains("signup_method") || body.Contains("onboarding_state")))
        {
            return Json(new JsonObject { ["code"] = "42703", ["message"] = "column does not exist" }, HttpStatusCode.BadRequest);
        }

        if (LegacySchema && path.StartsWith("/rest/v1/profiles?select=user_id,username", StringComparison.Ordinal))
        {
            var legacy = Profile.DeepClone().AsObject();
            legacy.Remove("onboarding_state");
            legacy.Remove("signup_method");
            legacy.Remove("has_password");
            return Json(new JsonArray(legacy));
        }

        if (path.StartsWith("/rest/v1/profiles?select=user_id,username", StringComparison.Ordinal))
        {
            return Json(new JsonArray(Profile.DeepClone()));
        }

        if (path.StartsWith("/rest/v1/profiles?select=user_id&email=eq.player", StringComparison.Ordinal))
        {
            return Json(new JsonArray(new JsonObject { ["user_id"] = UserId }));
        }

        if (path.StartsWith("/rest/v1/profiles?select=", StringComparison.Ordinal)) return Json(new JsonArray());

        if (request.Method == HttpMethod.Patch && path.StartsWith("/rest/v1/profiles", StringComparison.Ordinal))
        {
            foreach (var (key, value) in JsonNode.Parse(body)!.AsObject())
            {
                Profile[key] = value?.DeepClone();
            }

            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        if (path.StartsWith("/auth/v1/admin/users", StringComparison.Ordinal) && request.Method == HttpMethod.Put)
        {
            return Json(User());
        }

        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
    }

    private JsonObject Token() => new()
    {
        ["access_token"] = "header.eyJzZXNzaW9uX2lkIjoic2Vzc2lvbi0xIn0.signature",
        ["refresh_token"] = "refresh",
        ["expires_in"] = 3600,
        ["user"] = User(),
    };

    private JsonObject User() => new()
    {
        ["id"] = UserId,
        ["email"] = "player@example.test",
        ["email_confirmed_at"] = ProviderNow.ToString("O"),
        ["identities"] = Identities.DeepClone(),
    };

    private static HttpResponseMessage Json(JsonNode node, HttpStatusCode status = HttpStatusCode.OK)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(node.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        response.Headers.Date = ProviderNow;
        return response;
    }
}

internal sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}

public sealed class SupabaseAuthGatewayTests
{
    private readonly FakeSupabase supabase = new();

    private SupabaseAuthGateway Gateway(bool registrationEnabled = true, EmailCodeThrottle? throttle = null) => new(
        new FakeHttpClientFactory(supabase),
        new SupabaseAuthOptions
        {
            Url = "https://project.supabase.test",
            AnonKey = "anon",
            ServiceRoleKey = "service",
            GoogleEnabled = true,
            GoogleRedirectUrl = "http://localhost:5173/api/auth/google/callback",
            RegistrationEnabled = registrationEnabled,
        },
        throttle ?? new EmailCodeThrottle(),
        NullLogger<SupabaseAuthGateway>.Instance);

    [Fact]
    public async Task New_google_user_gets_a_session_that_still_needs_a_username()
    {
        var (session, refreshToken) = await Gateway().CompleteGoogleSignInAsync("code", "verifier", default);

        Assert.Equal(AccountOnboarding.UsernameRequired, session.Account.Onboarding);
        Assert.Null(session.Account.Username);
        Assert.False(session.Account.HasPassword);
        Assert.Equal("refresh", refreshToken);
        Assert.Contains("POST /auth/v1/token?grant_type=pkce", supabase.Paths);
        var exchange = supabase.Requests.Single(request => request.PathAndQuery == "/auth/v1/token?grant_type=pkce");
        Assert.Contains("\"code_verifier\":\"verifier\"", exchange.Body);
        Assert.Contains("google_sign_in", supabase.AuditEvents);
    }

    [Fact]
    public async Task Revoked_provider_session_is_reported_as_expired_not_forbidden()
    {
        supabase.SessionRevoked = true;

        var error = await Assert.ThrowsAsync<SupabaseAuthException>(
            () => Gateway().ValidateAccessTokenAsync("revoked-token", default));

        Assert.Equal(401, error.StatusCode);
        Assert.Equal("SESSION_EXPIRED", error.Code);
    }

    [Fact]
    public async Task Locked_account_is_rejected_and_its_new_session_revoked()
    {
        supabase.Profile["status"] = "locked";

        var error = await Assert.ThrowsAsync<SupabaseAuthException>(
            () => Gateway().CompleteGoogleSignInAsync("code", "verifier", default));

        Assert.Equal(423, error.StatusCode);
        Assert.Contains("POST /auth/v1/logout?scope=local", supabase.Paths);
    }

    [Fact]
    public async Task Unverified_password_profile_is_reclaimed_for_the_google_owner()
    {
        supabase.Profile["username"] = "squatter";
        supabase.Profile["onboarding_state"] = "complete";
        supabase.Profile["signup_method"] = "password";
        supabase.Profile["has_password"] = true;

        var (session, _) = await Gateway().CompleteGoogleSignInAsync("code", "verifier", default);

        Assert.Equal(AccountOnboarding.UsernameRequired, session.Account.Onboarding);
        Assert.Null(session.Account.Username);
        Assert.False(session.Account.HasPassword);
        Assert.Contains($"PUT /auth/v1/admin/users/{FakeSupabase.UserId}", supabase.Paths);
        Assert.Contains("unverified_profile_reclaimed", supabase.AuditEvents);
    }

    [Fact]
    public async Task Verified_password_account_is_entered_and_the_link_audited()
    {
        supabase.Profile["username"] = "player";
        supabase.Profile["onboarding_state"] = "complete";
        supabase.Profile["signup_method"] = "password";
        supabase.Profile["has_password"] = true;
        supabase.Identities =
        [
            FakeSupabase.Identity("email", FakeSupabase.ProviderNow.AddDays(-30)),
            FakeSupabase.Identity("google", FakeSupabase.ProviderNow.AddMinutes(-1)),
        ];

        var (session, _) = await Gateway().CompleteGoogleSignInAsync("code", "verifier", default);

        Assert.Equal("player", session.Account.Username);
        Assert.Equal(AccountOnboarding.Complete, session.Account.Onboarding);
        Assert.Contains("google_identity_linked", supabase.AuditEvents);
        Assert.DoesNotContain(supabase.Paths, path => path.StartsWith("PUT /auth/v1/admin/users", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Old_google_link_is_not_reported_again()
    {
        supabase.Profile["username"] = "player";
        supabase.Profile["onboarding_state"] = "complete";
        supabase.Profile["signup_method"] = "password";
        supabase.Identities =
        [
            FakeSupabase.Identity("email", FakeSupabase.ProviderNow.AddDays(-30)),
            FakeSupabase.Identity("google", FakeSupabase.ProviderNow.AddDays(-2)),
        ];

        await Gateway().CompleteGoogleSignInAsync("code", "verifier", default);

        Assert.DoesNotContain("google_identity_linked", supabase.AuditEvents);
    }

    [Fact]
    public async Task Unfinished_registration_is_deleted_while_registration_is_closed()
    {
        var error = await Assert.ThrowsAsync<SupabaseAuthException>(
            () => Gateway(registrationEnabled: false).CompleteGoogleSignInAsync("code", "verifier", default));

        Assert.Equal("REGISTRATION_DISABLED", error.Code);
        Assert.Contains($"DELETE /auth/v1/admin/users/{FakeSupabase.UserId}", supabase.Paths);
    }

    [Fact]
    public async Task Completed_account_signs_in_while_registration_is_closed()
    {
        supabase.Profile["username"] = "player";
        supabase.Profile["onboarding_state"] = "complete";

        var (session, _) = await Gateway(registrationEnabled: false).CompleteGoogleSignInAsync("code", "verifier", default);

        Assert.Equal("player", session.Account.Username);
        Assert.DoesNotContain(supabase.Paths, path => path.StartsWith("DELETE", StringComparison.Ordinal));
    }

    [Fact]
    public void Authorize_url_carries_state_inside_redirect_and_an_S256_challenge()
    {
        var flow = new GoogleOAuthFlowStore().Create(GoogleOAuthClient.Web)!;
        var url = new Uri(Gateway().BuildGoogleAuthorizeUrl(flow));
        var query = System.Web.HttpUtility.ParseQueryString(url.Query);

        Assert.Equal("/auth/v1/authorize", url.AbsolutePath);
        Assert.Equal("google", query["provider"]);
        Assert.Equal("s256", query["code_challenge_method"]);
        Assert.Equal(flow.CodeChallenge, query["code_challenge"]);
        Assert.Equal($"http://localhost:5173/api/auth/google/callback?state={flow.State}", query["redirect_to"]);
        Assert.DoesNotContain(flow.CodeVerifier, url.ToString());
        Assert.DoesNotContain(flow.FlowId, url.ToString());
    }

    [Fact]
    public async Task Forgot_password_is_silent_for_unknown_accounts()
    {
        await Gateway().SendRecoveryCodeAsync("nobody", default);

        Assert.DoesNotContain("POST /auth/v1/recover", supabase.Paths);
    }

    [Fact]
    public async Task Forgot_password_sends_once_per_cooldown()
    {
        var gateway = Gateway();
        await gateway.SendRecoveryCodeAsync("player@example.test", default);
        await gateway.SendRecoveryCodeAsync("player@example.test", default);

        Assert.Single(supabase.Paths, path => path == "POST /auth/v1/recover");
    }

    [Fact]
    public async Task Email_quota_exhaustion_is_reported_instead_of_claiming_a_code_was_sent()
    {
        supabase.RecoverStatus = HttpStatusCode.TooManyRequests;

        var error = await Assert.ThrowsAsync<SupabaseAuthException>(
            () => Gateway().SendRecoveryCodeAsync("player@example.test", default));

        Assert.Equal(429, error.StatusCode);
        Assert.Equal("EMAIL_RATE_LIMITED", error.Code);
    }

    [Fact]
    public async Task Reset_with_a_wrong_code_counts_toward_the_attempt_limit()
    {
        supabase.VerifyStatus = HttpStatusCode.Forbidden;
        var gateway = Gateway();

        for (var attempt = 0; attempt < EmailCodeThrottle.MaxFailedAttempts; attempt++)
        {
            var wrong = await Assert.ThrowsAsync<SupabaseAuthException>(
                () => gateway.VerifyRecoveryCodeAsync("player@example.test", "000000", default));
            Assert.Equal("CODE_INVALID", wrong.Code);
        }

        var blocked = await Assert.ThrowsAsync<SupabaseAuthException>(
            () => gateway.VerifyRecoveryCodeAsync("player@example.test", "123456", default));
        Assert.Equal("CODE_ATTEMPTS_EXCEEDED", blocked.Code);
    }

    [Fact]
    public async Task Reset_sets_the_password_and_keeps_only_the_new_session()
    {
        supabase.Profile["username"] = "player";
        supabase.Profile["onboarding_state"] = "complete";

        var gateway = Gateway();
        var (userId, email) = await gateway.VerifyRecoveryCodeAsync("player@example.test", "123456", default);

        // Verifying the code alone changes nothing and drops the recovery session.
        Assert.DoesNotContain($"PUT /auth/v1/admin/users/{FakeSupabase.UserId}", supabase.Paths);
        Assert.Contains("POST /auth/v1/logout?scope=local", supabase.Paths);
        Assert.Equal((FakeSupabase.UserId, "player@example.test"), (userId, email));

        await gateway.CompletePasswordResetAsync(userId, email, "a-new-password-123", default);

        Assert.Contains($"PUT /auth/v1/admin/users/{FakeSupabase.UserId}", supabase.Paths);
        Assert.True(supabase.Profile["has_password"]!.GetValue<bool>());
        Assert.Contains("POST /auth/v1/logout?scope=others", supabase.Paths);
        Assert.Contains("password_reset", supabase.AuditEvents);
    }

    [Fact]
    public async Task Reauth_verification_revokes_the_extra_session()
    {
        var account = new AuthAccountDto(
            FakeSupabase.UserId, "player", "player@example.test", "user", "active",
            true, false, false, AccountOnboarding.Complete, true);

        await Gateway().VerifyReauthCodeAsync(account, "123456", default);

        var verify = supabase.Requests.Single(request => request.PathAndQuery == "/auth/v1/verify");
        Assert.Equal("email", JsonNode.Parse(verify.Body)!["type"]!.GetValue<string>());
        Assert.Contains("POST /auth/v1/logout?scope=local", supabase.Paths);
    }

    [Fact]
    public async Task Password_accounts_keep_working_before_the_onboarding_migration()
    {
        supabase.LegacySchema = true;
        supabase.Profile["username"] = "player";
        supabase.Profile["must_change_password"] = true;
        var gateway = Gateway();

        var (session, _) = await gateway.LoginAsync("player@example.test", "old-password", default);
        Assert.Equal(AccountOnboarding.Complete, session.Account.Onboarding);
        Assert.True(session.Account.HasPassword);

        await gateway.ChangePasswordAsync(session.Account, "a-new-password-123", "old-password", default);
        Assert.False(supabase.Profile["must_change_password"]!.GetValue<bool>());
        Assert.DoesNotContain(
            supabase.Requests,
            request => request.Method == HttpMethod.Patch && request.Body.Contains("has_password"));
    }

    [Fact]
    public void Session_id_comes_from_the_access_token_claim() =>
        Assert.Equal(
            "session-1",
            SupabaseAuthGateway.SessionIdFromAccessToken("header.eyJzZXNzaW9uX2lkIjoic2Vzc2lvbi0xIn0.signature"));

    [Fact]
    public void Missing_session_claim_falls_back_to_a_token_hash()
    {
        var first = SupabaseAuthGateway.SessionIdFromAccessToken("not-a-jwt");
        Assert.Equal(64, first.Length);
        Assert.NotEqual(first, SupabaseAuthGateway.SessionIdFromAccessToken("another-token"));
    }
}
