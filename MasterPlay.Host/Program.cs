using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Globalization;
using System.Net.Mail;
using MasterPlay.Core.Entities;
using MasterPlay.Infrastructure.Data;
using MasterPlay.Infrastructure.Data.Services;
using MasterPlay.Infrastructure.Configuration;
using MasterPlay.Infrastructure.TrayControl;
using MasterPlay.Infrastructure.Integrations.Cloudinary;
using MasterPlay.Infrastructure.Integrations.Igdb;
using MasterPlay.Infrastructure.Integrations.Images;
using MasterPlay.Infrastructure.Integrations.Igdb.Models;
using MasterPlay.Infrastructure.Integrations.Rawg;
using MasterPlay.Infrastructure.Integrations.Steam;
using MasterPlay.Host;
using MasterPlay.Host.Auth;
using MasterPlay.Host.Integrations;
using MasterPlay.Host.Metadata;
using MasterPlay.Host.Notifications;
using MasterPlay.Host.Taxonomy;
using MasterPlay.Core.Taxonomy;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Local desktop setup: read secrets once from the per-user AppData folder.
// Environment variables remain supported and take precedence for deployment/CI.
// AUTH_SETUP.md documents %APPDATA%\QuestLog; choosing another data folder at
// install time must not silently drop accounts, so read both (data folder wins).
var authConfigDirectories = new[] { AppPaths.DefaultDataDirectory, AppPaths.GetDataDirectory() }
    .Where(Directory.Exists)
    .Distinct(StringComparer.OrdinalIgnoreCase);
foreach (var authConfigDirectory in authConfigDirectories)
{
    builder.Configuration.AddJsonFile(
        new PhysicalFileProvider(authConfigDirectory),
        "auth.local.json",
        optional: true,
        reloadOnChange: false);
}
// WebApplicationBuilder registers environment variables before custom JSON
// providers. Register them once more so an explicitly supplied deployment
// secret always wins over a stale per-user auth.local.json value.
builder.Configuration.AddEnvironmentVariables();

DataService.EnsureInitialized();

if (args.Any(argument => string.Equals(argument, "--sync-missing-posters", StringComparison.OrdinalIgnoreCase)))
{
    Environment.ExitCode = await SyncMissingPostersAsync(builder.Configuration);
    return;
}

if (args.Any(argument => string.Equals(argument, "--sync-cloudinary-images", StringComparison.OrdinalIgnoreCase)))
{
    Environment.ExitCode = await SyncImagesAsync();
    return;
}

builder.Services.AddDbContext<MasterPlayContext>();

var authOptions = SupabaseAuthOptions.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(authOptions);
builder.Services.AddSingleton<DesktopGuestSession>();
builder.Services.AddSingleton(new GoogleOAuthFlowStore());
builder.Services.AddSingleton(new ReauthTicketStore());
builder.Services.AddSingleton(new PasswordResetTicketStore());
builder.Services.AddSingleton(new EmailCodeThrottle());
builder.Services.AddHttpClient(nameof(SupabaseAuthGateway), client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddHttpClient(nameof(SupabaseMetadataCatalog), client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddHttpClient(nameof(SupabaseMetadataAdmin), client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
});
builder.Services.AddHttpClient(nameof(SupabaseIntegrationStore), client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddHttpClient(nameof(IntegrationConnectionTester), client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddSingleton<SupabaseAuthGateway>();
builder.Services.AddSingleton<SupabaseMetadataCatalog>();
builder.Services.AddSingleton<IGameMetadataProviders, LiveGameMetadataProviders>();
builder.Services.AddSingleton<GameMetadataPreviewService>();
builder.Services.AddSingleton<SupabaseMetadataAdmin>();
builder.Services.AddSingleton<SupabaseIntegrationStore>();
builder.Services.AddSingleton<IntegrationConnectionTester>();
builder.Services.AddHostedService<BootstrapAdminService>();
builder.Services.AddHostedService<IntegrationCredentialWarmupService>();
builder.Services.AddSingleton<ImageAutoSyncQueue>();
builder.Services.AddHostedService<ImageAutoSyncService>();
builder.Services.AddRateLimiter(rateLimiterOptions =>
{
    rateLimiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    rateLimiterOptions.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));
    // Google sign-in polls /complete every 1.5 s while the user is in the browser,
    // which the 10/min "auth" policy would block. Each poll is a memory lookup.
    rateLimiterOptions.AddPolicy("auth-poll", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));
    rateLimiterOptions.AddPolicy("integration-test", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));
});

// CORS chi cho origin cua Vite dev server. Moi domain endpoint ben duoi duoc
// auth middleware xac thuc bang Supabase access token truoc khi chay.
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

var app = builder.Build();
app.UseCors();
app.UseRateLimiter();

app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    var isApi = path.StartsWithSegments("/api");
    var isPublic = path == "/api/health" ||
                   path == "/api/auth/config" ||
                   path == "/api/auth/login" ||
                   path == "/api/auth/signup" ||
                   path == "/api/auth/signup/resend" ||
                   path == "/api/auth/signup/verify" ||
                   path == "/api/auth/guest" ||
                   path == "/api/auth/session" ||
                   path == "/api/auth/logout" ||
                   path == "/api/auth/google/start" ||
                   path == "/api/auth/google/callback" ||
                   path == "/api/auth/google/complete" ||
                   path == "/api/auth/password/forgot" ||
                   path == "/api/auth/password/verify-code" ||
                   path == "/api/auth/password/reset";

    if (!isApi || isPublic)
    {
        await next();
        return;
    }

    var accessToken = BearerToken(context.Request);
    var desktopGuest = context.RequestServices.GetRequiredService<DesktopGuestSession>();
    if (desktopGuest.AuthorizeBearer(accessToken, context.Connection.RemoteIpAddress))
    {
        context.Items["QuestLogGuest"] = true;
        await next();
        return;
    }

    if (!authOptions.IsConfigured)
    {
        await WriteAuthError(
            context,
            StatusCodes.Status503ServiceUnavailable,
            "AUTH_NOT_CONFIGURED",
            "QuestLog authentication is not configured on this server.");
        return;
    }

    if (string.IsNullOrWhiteSpace(accessToken))
    {
        await WriteAuthError(
            context,
            StatusCodes.Status401Unauthorized,
            "AUTH_REQUIRED",
            "Sign in to continue.");
        return;
    }

    try
    {
        var gateway = context.RequestServices.GetRequiredService<SupabaseAuthGateway>();
        var account = await gateway.ValidateAccessTokenAsync(
            accessToken,
            context.RequestAborted);
        context.Items["QuestLogAccount"] = account;
        // A Google account without a username reaches nothing but the auth
        // routes it needs to finish registration (design §7). The frontend gate
        // is only a convenience.
        if (account.Onboarding == AccountOnboarding.UsernameRequired && !AllowedDuringOnboarding(context.Request))
        {
            await WriteAuthError(
                context,
                StatusCodes.Status403Forbidden,
                "USERNAME_REQUIRED",
                "Choose a username to finish creating your account.");
            return;
        }
        var isAdminAccount = string.Equals(account.Role, "admin", StringComparison.OrdinalIgnoreCase);
        // Tray/CLI dong bo anh khong biet ai dang nhap: ghi lai chu nao la admin
        // de anh cua chu do len Cloudinary, con lai luu tren may (DOMAIN_RULES §12).
        await ImageStorageOwners.RecordAsync($"user:{account.Id}", isAdminAccount, context.RequestAborted);
        if (isAdminAccount)
        {
            await ClaimLegacyGamesAsync(
                context.RequestServices.GetRequiredService<MasterPlayContext>(),
                account.Id,
                context.RequestAborted);
        }
        await next();
    }
    catch (SupabaseAuthException exception)
    {
        await WriteAuthError(context, exception.StatusCode, exception.Code, exception.Message);
    }
});

app.UseBundledWebUi();

/*
 * Anh cua thu vien khong thuoc admin, luu tren may (LocalImageStore). Nam NGOAI
 * /api vi the <img> khong gui bearer token. Ten file chua id game (GUID) + hash
 * noi dung nen khong doan duoc; Host chi nghe localhost. Ten file bi kiem tra
 * chat (khong '/', khong '..') truoc khi mo; noi dung chi la anh raster da
 * kiem magic bytes luc ghi, kem nosniff.
 */
app.MapGet("/local-images/{fileName}", (HttpContext context, string fileName) =>
{
    if (!LocalImageStore.TryResolveFile(LocalImageStore.UrlPrefix + fileName, out var path))
        return Results.NotFound();

    context.Response.Headers.XContentTypeOptions = "nosniff";
    // Doi anh thi doi ten file (hash noi dung) nen cache vinh vien duoc.
    context.Response.Headers.CacheControl = "private, max-age=31536000, immutable";
    return Results.File(path, LocalImageStore.ContentType(fileName));
});

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", db = JsonService.GetDatabasePath() }));

app.MapGet("/api/auth/config", () => Results.Ok(new AuthConfigDto(
    authOptions.IsConfigured,
    authOptions.RegistrationEnabled,
    authOptions.GoogleReady)));

app.MapPost("/api/auth/login", async (
    HttpContext context,
    SupabaseAuthGateway gateway,
    LoginRequestDto body,
    CancellationToken cancellationToken) =>
{
    var identifier = body.Identifier?.Trim();
    if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(body.Password))
    {
        return AuthErrorResult(400, "LOGIN_FIELDS_REQUIRED", "Enter your username/email and password.");
    }

    try
    {
        var result = await gateway.LoginAsync(identifier, body.Password, cancellationToken);
        SetRefreshCookie(context, result.RefreshToken);
        return Results.Ok(result.Session);
    }
    catch (SupabaseAuthException exception)
    {
        return AuthErrorResult(exception.StatusCode, exception.Code, exception.Message);
    }
}).RequireRateLimiting("auth");

app.MapPost("/api/auth/signup", async (
    SupabaseAuthGateway gateway,
    SignupRequestDto body,
    CancellationToken cancellationToken) =>
{
    var username = body.Username?.Trim() ?? string.Empty;
    var email = body.Email?.Trim() ?? string.Empty;
    var password = body.Password ?? string.Empty;

    if (username.Length is < 3 or > 32 ||
        username.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '_' or '.' or '-')))
    {
        return AuthErrorResult(
            400,
            "INVALID_USERNAME",
            "Username must be 3-32 characters using letters, numbers, dot, dash, or underscore.");
    }

    if (!email.Contains('@') || password.Length < 8)
    {
        return AuthErrorResult(
            400,
            "INVALID_SIGNUP",
            "Enter a valid email and a password with at least 8 characters.");
    }

    try
    {
        return Results.Ok(await gateway.SignupAsync(username, email, password, cancellationToken));
    }
    catch (SupabaseAuthException exception)
    {
        return AuthErrorResult(exception.StatusCode, exception.Code, exception.Message);
    }
}).RequireRateLimiting("auth");

// Xac nhan dang ky bang ma 6 so: ban desktop khong co trang web nao de mo link
// xac nhan, nen email "Confirm signup" chua {{ .Token }} thay vi link.
app.MapPost("/api/auth/signup/resend", async (
    SupabaseAuthGateway gateway,
    SignupCodeRequestDto body,
    CancellationToken cancellationToken) =>
{
    var identifier = body.Identifier?.Trim() ?? string.Empty;
    if (identifier.Length == 0)
    {
        return AuthErrorResult(400, "IDENTIFIER_REQUIRED", "Enter your username or email.");
    }

    try
    {
        var (sent, resendAvailableAt) = await gateway.ResendSignupCodeAsync(identifier, cancellationToken);
        if (!sent)
        {
            return Results.Json(
                new
                {
                    error = new
                    {
                        code = "CODE_RESEND_COOLDOWN",
                        message = "Wait before requesting another code.",
                        details = new { resendAvailableAt },
                    },
                },
                statusCode: StatusCodes.Status429TooManyRequests);
        }

        return Results.Json(
            new SignupCodeResponseDto(true, resendAvailableAt),
            statusCode: StatusCodes.Status202Accepted);
    }
    catch (SupabaseAuthException exception)
    {
        return AuthErrorResult(exception.StatusCode, exception.Code, exception.Message);
    }
}).RequireRateLimiting("auth");

app.MapPost("/api/auth/signup/verify", async (
    HttpContext context,
    SupabaseAuthGateway gateway,
    VerifySignupCodeRequestDto body,
    CancellationToken cancellationToken) =>
{
    var identifier = body.Identifier?.Trim() ?? string.Empty;
    var code = body.Code?.Trim() ?? string.Empty;
    if (identifier.Length == 0 || !IsEmailCode(code))
    {
        return AuthErrorResult(400, "CODE_INVALID", "The code is wrong or has expired.");
    }

    try
    {
        var result = await gateway.VerifySignupCodeAsync(identifier, code, cancellationToken);
        SetRefreshCookie(context, result.RefreshToken);
        return Results.Ok(result.Session);
    }
    catch (SupabaseAuthException exception)
    {
        return AuthErrorResult(exception.StatusCode, exception.Code, exception.Message);
    }
}).RequireRateLimiting("auth");

app.MapPost("/api/auth/guest", (
    HttpContext context,
    DesktopGuestSession desktopGuest) =>
{
    if (!desktopGuest.AuthorizeExchange(context))
    {
        return AuthErrorResult(
            403,
            "DESKTOP_GUEST_UNAVAILABLE",
            "Guest access is available only inside the QuestLog desktop app.");
    }

    return Results.Ok(desktopGuest.CreateSession());
}).RequireRateLimiting("auth");

app.MapPost("/api/auth/session", async (
    HttpContext context,
    SupabaseAuthGateway gateway,
    CancellationToken cancellationToken) =>
{
    if (!context.Request.Cookies.TryGetValue("questlog_refresh_token", out var refreshToken) ||
        string.IsNullOrWhiteSpace(refreshToken))
    {
        return AuthErrorResult(401, "SESSION_MISSING", "Sign in to continue.");
    }

    try
    {
        var result = await gateway.RefreshAsync(refreshToken, cancellationToken);
        SetRefreshCookie(context, result.RefreshToken);
        return Results.Ok(result.Session);
    }
    catch (SupabaseAuthException exception)
    {
        ClearRefreshCookie(context);
        return AuthErrorResult(exception.StatusCode, exception.Code, exception.Message);
    }
}).RequireRateLimiting("auth");

app.MapGet("/api/auth/me", (HttpContext context) =>
    context.Items.TryGetValue("QuestLogAccount", out var account)
        ? Results.Ok(account)
        : AuthErrorResult(401, "AUTH_REQUIRED", "Sign in to continue."));

const string OAuthFlowCookie = "questlog_oauth_flow";

/*
 * Doi mat khau. Mac dinh can reauth ticket (ma 6 so qua email). Ngoai le duy
 * nhat: bootstrap admin dang bi ep doi mat khau lan dau van dung mat khau hien
 * tai — Host kiem tra dieu kien nay, client khong chon duoc.
 */
app.MapPost("/api/auth/change-password", async (
    HttpContext context,
    SupabaseAuthGateway gateway,
    ReauthTicketStore tickets,
    ChangePasswordRequestDto body,
    CancellationToken cancellationToken) =>
{
    var newPassword = body.NewPassword ?? string.Empty;
    if (newPassword.Length < 12)
    {
        return AuthErrorResult(400, "INVALID_PASSWORD_CHANGE", "Use a new password with at least 12 characters.");
    }

    if (context.Items["QuestLogAccount"] is not AuthAccountDto account ||
        BearerToken(context.Request) is not { Length: > 0 } accessToken)
    {
        return AuthErrorResult(401, "AUTH_REQUIRED", "Sign in to continue.");
    }

    if (account.MustChangePassword && !string.IsNullOrEmpty(body.CurrentPassword))
    {
        if (string.Equals(body.CurrentPassword, newPassword, StringComparison.Ordinal))
        {
            return AuthErrorResult(400, "PASSWORD_UNCHANGED", "Choose a different password.");
        }

        try
        {
            var result = await gateway.ChangePasswordAsync(account, newPassword, body.CurrentPassword, cancellationToken);
            SetRefreshCookie(context, result.RefreshToken);
            return Results.Ok(result.Session);
        }
        catch (SupabaseAuthException exception)
        {
            return AuthErrorResult(exception.StatusCode, exception.Code, exception.Message);
        }
    }

    return await WithReauthTicket(context, tickets, account, accessToken, body.ReauthTicket, async () =>
    {
        var result = await gateway.ChangePasswordAsync(account, newPassword, null, cancellationToken);
        SetRefreshCookie(context, result.RefreshToken);
        return Results.Ok(result.Session);
    });
}).RequireRateLimiting("auth");

app.MapPatch("/api/auth/account/username", async (
    HttpContext context,
    SupabaseAuthGateway gateway,
    ReauthTicketStore tickets,
    ChangeUsernameRequestDto body,
    CancellationToken cancellationToken) =>
{
    var username = body.Username?.Trim() ?? string.Empty;
    if (!IsValidUsername(username))
    {
        return InvalidUsernameResult();
    }

    if (context.Items["QuestLogAccount"] is not AuthAccountDto account ||
        BearerToken(context.Request) is not { Length: > 0 } accessToken)
    {
        return AuthErrorResult(401, "AUTH_REQUIRED", "Sign in to continue.");
    }

    return await WithReauthTicket(context, tickets, account, accessToken, body.ReauthTicket, async () =>
        Results.Ok(await gateway.ChangeUsernameAsync(account, username, cancellationToken)));
}).RequireRateLimiting("auth");

app.MapPost("/api/auth/account/email-change", async (
    HttpContext context,
    SupabaseAuthGateway gateway,
    ReauthTicketStore tickets,
    ChangeEmailRequestDto body,
    CancellationToken cancellationToken) =>
{
    var email = body.Email?.Trim() ?? string.Empty;
    if (!MailAddress.TryCreate(email, out var parsedEmail) ||
        !string.Equals(parsedEmail.Address, email, StringComparison.OrdinalIgnoreCase))
    {
        return AuthErrorResult(400, "INVALID_EMAIL", "Enter a valid email address.");
    }

    if (context.Items["QuestLogAccount"] is not AuthAccountDto account ||
        BearerToken(context.Request) is not { Length: > 0 } accessToken)
    {
        return AuthErrorResult(401, "AUTH_REQUIRED", "Sign in to continue.");
    }

    if (string.Equals(account.Email, email, StringComparison.OrdinalIgnoreCase))
    {
        return AuthErrorResult(400, "EMAIL_UNCHANGED", "Choose a different email address.");
    }

    return await WithReauthTicket(context, tickets, account, accessToken, body.ReauthTicket, async () =>
        Results.Ok(await gateway.ChangeEmailAsync(account, accessToken, email, cancellationToken)));
}).RequireRateLimiting("auth");

/* ---------- Ma 6 so qua email: quen mat khau + xac thuc lai ---------- */

// Luon tra 202 — ke ca khi tai khoan khong ton tai hoac dang trong cooldown —
// de endpoint public khong tiet lo tai khoan nao co that.
app.MapPost("/api/auth/password/forgot", async (
    SupabaseAuthGateway gateway,
    ForgotPasswordRequestDto body,
    CancellationToken cancellationToken) =>
{
    var identifier = body.Identifier?.Trim() ?? string.Empty;
    if (identifier.Length == 0)
    {
        return AuthErrorResult(400, "IDENTIFIER_REQUIRED", "Enter your username or email.");
    }

    try
    {
        await gateway.SendRecoveryCodeAsync(identifier, cancellationToken);
        return Results.Json(new ForgotPasswordResponseDto(true), statusCode: StatusCodes.Status202Accepted);
    }
    catch (SupabaseAuthException exception)
    {
        return AuthErrorResult(exception.StatusCode, exception.Code, exception.Message);
    }
}).RequireRateLimiting("auth");

// Buoc 2 cua quen mat khau: kiem tra ma NGAY, dung thi cap reset ticket de sang
// man dat mat khau moi. Supabase huy ma khi verify, nen buoc sau dung ticket.
app.MapPost("/api/auth/password/verify-code", async (
    SupabaseAuthGateway gateway,
    PasswordResetTicketStore resetTickets,
    VerifyResetCodeRequestDto body,
    CancellationToken cancellationToken) =>
{
    var identifier = body.Identifier?.Trim() ?? string.Empty;
    var code = body.Code?.Trim() ?? string.Empty;
    if (identifier.Length == 0 || !IsEmailCode(code))
    {
        return AuthErrorResult(400, "CODE_INVALID", "The code is wrong or has expired.");
    }

    try
    {
        var (userId, email) = await gateway.VerifyRecoveryCodeAsync(identifier, code, cancellationToken);
        var (ticket, expiresAt) = resetTickets.Issue(userId, email);
        return Results.Ok(new ResetTicketResponseDto(ticket, expiresAt));
    }
    catch (SupabaseAuthException exception)
    {
        return AuthErrorResult(exception.StatusCode, exception.Code, exception.Message);
    }
}).RequireRateLimiting("auth");

// Buoc 3: dat mat khau moi bang reset ticket (dung mot lan, 10 phut).
app.MapPost("/api/auth/password/reset", async (
    HttpContext context,
    SupabaseAuthGateway gateway,
    PasswordResetTicketStore resetTickets,
    ResetPasswordRequestDto body,
    CancellationToken cancellationToken) =>
{
    var newPassword = body.NewPassword ?? string.Empty;
    if (newPassword.Length < 12)
    {
        return AuthErrorResult(400, "INVALID_PASSWORD_CHANGE", "Use a new password with at least 12 characters.");
    }

    var reservation = resetTickets.Reserve(body.ResetTicket);
    if (reservation is null)
    {
        return AuthErrorResult(401, "RESET_TICKET_INVALID", "This reset expired. Request a new code.");
    }

    try
    {
        var result = await gateway.CompletePasswordResetAsync(
            reservation.Entry.UserId,
            reservation.Entry.Email,
            newPassword,
            cancellationToken);
        SetRefreshCookie(context, result.RefreshToken);
        return Results.Ok(result.Session);
    }
    catch (SupabaseAuthException exception)
    {
        if (exception.StatusCode is >= 400 and < 500 and not 401 and not 423)
        {
            resetTickets.Release(reservation);
        }
        return AuthErrorResult(exception.StatusCode, exception.Code, exception.Message);
    }
}).RequireRateLimiting("auth");

app.MapPost("/api/auth/reauth/email", async (
    HttpContext context,
    SupabaseAuthGateway gateway,
    CancellationToken cancellationToken) =>
{
    if (context.Items["QuestLogAccount"] is not AuthAccountDto account)
    {
        return AuthErrorResult(401, "AUTH_REQUIRED", "Sign in to continue.");
    }

    try
    {
        var (sent, resendAvailableAt) = await gateway.SendReauthCodeAsync(account, cancellationToken);
        if (!sent)
        {
            return Results.Json(
                new
                {
                    error = new
                    {
                        code = "CODE_RESEND_COOLDOWN",
                        message = "Wait before requesting another code.",
                        details = new { resendAvailableAt },
                    },
                },
                statusCode: StatusCodes.Status429TooManyRequests);
        }

        return Results.Json(
            new ReauthCodeResponseDto(true, resendAvailableAt),
            statusCode: StatusCodes.Status202Accepted);
    }
    catch (SupabaseAuthException exception)
    {
        return AuthErrorResult(exception.StatusCode, exception.Code, exception.Message);
    }
}).RequireRateLimiting("auth");

app.MapPost("/api/auth/reauth/verify", async (
    HttpContext context,
    SupabaseAuthGateway gateway,
    ReauthTicketStore tickets,
    ReauthVerifyRequestDto body,
    CancellationToken cancellationToken) =>
{
    if (context.Items["QuestLogAccount"] is not AuthAccountDto account ||
        BearerToken(context.Request) is not { Length: > 0 } accessToken)
    {
        return AuthErrorResult(401, "AUTH_REQUIRED", "Sign in to continue.");
    }

    var code = body.Code?.Trim() ?? string.Empty;
    if (!IsEmailCode(code))
    {
        return AuthErrorResult(400, "CODE_INVALID", "The code is wrong or has expired.");
    }

    try
    {
        await gateway.VerifyReauthCodeAsync(account, code, cancellationToken);
        var (ticket, expiresAt) = tickets.Issue(
            account.Id,
            SupabaseAuthGateway.SessionIdFromAccessToken(accessToken));
        return Results.Ok(new ReauthTicketResponseDto(ticket, expiresAt));
    }
    catch (SupabaseAuthException exception)
    {
        return AuthErrorResult(exception.StatusCode, exception.Code, exception.Message);
    }
}).RequireRateLimiting("auth");

/* ---------- Dang ky / dang nhap bang Google (Host lam trung gian PKCE) ---------- */

app.MapPost("/api/auth/google/start", (
    HttpContext context,
    SupabaseAuthGateway gateway,
    GoogleOAuthFlowStore flows,
    DesktopGuestSession desktopGuest,
    GoogleStartRequestDto body) =>
{
    if (!authOptions.IsConfigured)
    {
        return AuthErrorResult(503, "AUTH_NOT_CONFIGURED", "QuestLog authentication is not configured on this server.");
    }

    if (!authOptions.GoogleReady)
    {
        return AuthErrorResult(403, "GOOGLE_DISABLED", "Google sign-in is not enabled.");
    }

    GoogleOAuthClient client;
    switch (body.Client)
    {
        case "web":
            client = GoogleOAuthClient.Web;
            break;
        case "desktop":
            // Chi WebView2 cua Shell co handshake token nay; trinh duyet thuong
            // va Hosted API khong khoi tao duoc flow desktop.
            if (!desktopGuest.AuthorizeExchange(context))
            {
                return AuthErrorResult(403, "DESKTOP_HANDSHAKE_REQUIRED", "Desktop sign-in is available only inside the QuestLog app.");
            }
            client = GoogleOAuthClient.Desktop;
            break;
        default:
            return AuthErrorResult(400, "INVALID_OAUTH_CLIENT", "Unknown sign-in client.");
    }

    var flow = flows.Create(client);
    if (flow is null)
    {
        return AuthErrorResult(429, "RATE_LIMITED", "Too many sign-in attempts in progress. Try again shortly.");
    }

    if (client == GoogleOAuthClient.Web)
    {
        SetOAuthFlowCookie(context, flow.FlowId);
    }

    return Results.Ok(new GoogleStartResponseDto(flow.FlowId, gateway.BuildGoogleAuthorizeUrl(flow), flow.ExpiresAt));
}).RequireRateLimiting("auth");

// Chi nhan code va gan vao flow, KHONG doi code o day: flow bi huy/bo do thi
// khong bao gio sinh ra phien. Tra trang tinh, khong redirect.
app.MapGet("/api/auth/google/callback", (HttpContext context, GoogleOAuthFlowStore flows) =>
{
    var query = context.Request.Query;
    var flow = flows.TakeByState(query["state"].ToString());
    var success = false;
    if (flow is not null)
    {
        var code = query["code"].ToString();
        var boundToBrowser = flow.Client != GoogleOAuthClient.Web ||
                             (context.Request.Cookies.TryGetValue(OAuthFlowCookie, out var cookieFlowId) &&
                              string.Equals(cookieFlowId, flow.FlowId, StringComparison.Ordinal));
        if (boundToBrowser && string.IsNullOrEmpty(query["error"].ToString()) && code.Length > 0)
        {
            flow.Authorize(code);
            success = true;
        }
        else
        {
            flow.Cancel();
        }
    }

    var vietnamese = context.Request.Headers.AcceptLanguage.ToString()
        .StartsWith("vi", StringComparison.OrdinalIgnoreCase);
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'";
    return Results.Content(GoogleCallbackPage(success, vietnamese), "text/html; charset=utf-8");
}).RequireRateLimiting("auth-poll");

app.MapPost("/api/auth/google/complete", async (
    HttpContext context,
    SupabaseAuthGateway gateway,
    GoogleOAuthFlowStore flows,
    DesktopGuestSession desktopGuest,
    GoogleCompleteRequestDto body,
    CancellationToken cancellationToken) =>
{
    var flow = flows.Get(body.FlowId);
    var bound = flow is not null && (flow.Client == GoogleOAuthClient.Desktop
        ? desktopGuest.AuthorizeExchange(context)
        : context.Request.Cookies.TryGetValue(OAuthFlowCookie, out var cookieFlowId) &&
          string.Equals(cookieFlowId, flow.FlowId, StringComparison.Ordinal));
    if (flow is null || !bound)
    {
        return AuthErrorResult(410, "OAUTH_FLOW_EXPIRED", "This sign-in attempt has expired. Start again.");
    }

    var (progress, code) = flow.TryBeginExchange();
    if (progress == GoogleFlowProgress.Pending)
    {
        return Results.Ok(new GoogleCompleteResponseDto("pending", null));
    }

    flows.Remove(flow);
    ClearOAuthFlowCookie(context);
    if (progress == GoogleFlowProgress.Cancelled)
    {
        return AuthErrorResult(400, "OAUTH_CANCELLED", "Google sign-in was cancelled or did not complete.");
    }

    try
    {
        var result = await gateway.CompleteGoogleSignInAsync(code!, flow.CodeVerifier, cancellationToken);
        SetRefreshCookie(context, result.RefreshToken);
        return Results.Ok(new GoogleCompleteResponseDto("completed", result.Session));
    }
    catch (SupabaseAuthException exception)
    {
        return AuthErrorResult(exception.StatusCode, exception.Code, exception.Message);
    }
}).RequireRateLimiting("auth-poll");

app.MapPost("/api/auth/account/username-claim", async (
    HttpContext context,
    SupabaseAuthGateway gateway,
    UsernameClaimRequestDto body,
    CancellationToken cancellationToken) =>
{
    var username = body.Username?.Trim() ?? string.Empty;
    if (!IsValidUsername(username))
    {
        return InvalidUsernameResult();
    }

    if (context.Items["QuestLogAccount"] is not AuthAccountDto account)
    {
        return AuthErrorResult(401, "AUTH_REQUIRED", "Sign in to continue.");
    }

    try
    {
        return Results.Ok(await gateway.ClaimUsernameAsync(account, username, cancellationToken));
    }
    catch (SupabaseAuthException exception)
    {
        return AuthErrorResult(exception.StatusCode, exception.Code, exception.Message);
    }
}).RequireRateLimiting("auth");

app.MapPost("/api/auth/logout", async (
    HttpContext context,
    SupabaseAuthGateway gateway,
    CancellationToken cancellationToken) =>
{
    var accessToken = BearerToken(context.Request);
    if (!string.IsNullOrWhiteSpace(accessToken))
    {
        // Only this device: a global sign-out would revoke the desktop app and every
        // other browser, which then fail mid-session with session_not_found.
        await gateway.LogoutAsync(accessToken, cancellationToken, "local");
    }

    ClearRefreshCookie(context);
    return Results.NoContent();
});

// Kieu tra ve phai ghi ro: lambda async chi nhan HttpContext se khop overload
// RequestDelegate, gia tri tra ve bi bo -> 200 body rong ("Unexpected end of JSON input").
app.MapGet("/api/capabilities", async Task<IResult> (HttpContext context) => Results.Ok(new CapabilitiesDto(
    // Local API chay tren may co game -> co day du kha nang native.
    // Cac endpoint native chua cai dat, se bo sung dan (xem UI_MAP.md §6).
    Mode: "local",
    Features: new CapabilityFeaturesDto(
        Launch: OperatingSystem.IsWindows(),
        FilePicker: OperatingSystem.IsWindows(),
        RevealFolder: OperatingSystem.IsWindows(),
        // Host noi vao tray qua named pipe (TrayControl). Tray co dang chay hay khong
        // thi hoi /api/tray/status.
        Tray: OperatingSystem.IsWindows(),
        Scanner: false,
        Controller: OperatingSystem.IsWindows(),
        // Dong bo anh chay ngay trong Host, khong can tien trinh Tray. Thu vien
        // khong thuoc admin luu anh tren may nen luon dong bo duoc; thu vien
        // admin can Cloudinary.
        ImageSync: !TryGetLibraryOwnerKey(context, out var capabilityOwner) ||
                   await ImageStorageOwners.UsesCloudAsync(capabilityOwner, context.RequestAborted) != true ||
                   CloudinaryImageService.IsConfigured,
        // Backup save game dang tam tat (MasterPlay.Core.FeatureFlags).
        Backup: MasterPlay.Core.FeatureFlags.GameBackup && OperatingSystem.IsWindows()))));

// Mot lan dong bo anh tai mot thoi diem: hai lan chong nhau se upload trung
// va tranh nhau ghi cung cot anh.
var imageSyncGate = new SemaphoreSlim(1, 1);

/*
 * Dua anh trong thu vien cua nguoi dang dang nhap ve noi luu: Cloudinary neu
 * la admin, con lai luu tren may (GameImageSync, DOMAIN_RULES §12).
 *
 * Moi luong ghi anh (them game tu kho, lookup metadata, form sua, lenh dien
 * poster IGDB) deu luu link GOC. Truoc day chi menu Tray goi duoc dong bo nen
 * link goc nam yen mai. Chay dong bo (khong 202 + SSE) vi Host chua co kenh
 * /api/events; bao cao tra ve kem LY DO tung anh loi.
 */
app.MapPost("/api/tray/sync-images", async (HttpContext context, CancellationToken cancellationToken) =>
{
    if (!TryGetLibraryOwnerKey(context, out var ownerKey))
        return AuthErrorResult(401, "AUTH_REQUIRED", "Sign in to continue.");
    if (await ImageStorageOwners.UsesCloudAsync(ownerKey, cancellationToken) == true && !CloudinaryImageService.IsConfigured)
    {
        return Results.Json(
            new { error = new { code = "INTEGRATION_NOT_CONFIGURED", message = "Cloudinary is not configured." } },
            statusCode: StatusCodes.Status422UnprocessableEntity);
    }
    if (!await imageSyncGate.WaitAsync(0, cancellationToken))
    {
        return Results.Json(
            new { error = new { code = "IMAGE_SYNC_RUNNING", message = "An image sync is already running." } },
            statusCode: StatusCodes.Status409Conflict);
    }

    try
    {
        var result = await GameImageSync.SyncAllAsync(
            ownerKey: ownerKey,
            cancellationToken: cancellationToken);
        return Results.Ok(new ImageSyncReportDto(
            TotalGames: result.TotalGames,
            Banner: new ImageSyncRoleCountsDto(result.CardCoverUploaded, result.CardCoverAlreadySynced, result.CardCoverMissing),
            Backdrop: new ImageSyncRoleCountsDto(result.CoverUploaded, result.CoverAlreadySynced, result.CoverMissing),
            Poster: new ImageSyncRoleCountsDto(result.PortraitCoverUploaded, result.PortraitCoverAlreadySynced, result.PortraitCoverMissing),
            Failed: result.Failed,
            Failures: result.Failures
                .OrderBy(failure => failure.GameName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(failure => failure.Role, StringComparer.Ordinal)
                .Select(failure => new ImageSyncFailureDto(failure.GameId, failure.GameName, failure.Role, failure.Reason))
                .ToList()));
    }
    finally
    {
        imageSyncGate.Release();
    }
});

/*
 * Panel "Dich vu nen" dieu khien tien trinh Tray (WinForms rieng, song khi UI dong)
 * qua named pipe cua user hien tai. Tray khong chay la mot TRANG THAI (status tra
 * running=false), con lenh gui toi tray khong chay thi la loi 409 TRAY_NOT_RUNNING.
 */
app.MapGet("/api/tray/status", async (CancellationToken cancellationToken) =>
{
    var reply = await TrayControlClient.SendAsync(
        new TrayControlRequest(TrayControlProtocol.Status), cancellationToken);
    return Results.Ok(reply?.Status is { } status
        ? ToTrayStatusDto(status)
        : new TrayStatusDto(false, [], TrayAutoStartEnabled()));
});

app.MapPost("/api/tray/services/{name}", async (string name, SetTrayServiceDto body, CancellationToken cancellationToken) =>
{
    if (name == "backup")
    {
        return Results.Json(
            TaxonomyError("FEATURE_DISABLED", "Game backup is temporarily disabled."),
            statusCode: StatusCodes.Status501NotImplemented);
    }
    if (name is not (TrayControlProtocol.Playtime or TrayControlProtocol.Scanner))
        return Results.BadRequest(TaxonomyError("INVALID_TRAY_SERVICE", "service must be playtime or scanner."));
    if (body.Enabled is not bool enabled)
        return Results.BadRequest(TaxonomyError("ENABLED_REQUIRED", "enabled is required."));

    return TrayCommandResult(
        await TrayControlClient.SendAsync(
            new TrayControlRequest(TrayControlProtocol.SetService, name, enabled), cancellationToken),
        StatusCodes.Status204NoContent);
});

app.MapPost("/api/tray/{action}", async (string action, CancellationToken cancellationToken) =>
{
    var command = action switch
    {
        "scan-now" => TrayControlProtocol.ScanNow,
        "open-logs" => TrayControlProtocol.OpenLogs,
        _ => null,
    };
    if (command == null)
    {
        return action is "backup-now" or "startup"
            ? Results.Json(
                TaxonomyError("NOT_IMPLEMENTED", $"Tray action '{action}' is not available yet."),
                statusCode: StatusCodes.Status501NotImplemented)
            : Results.NotFound(TaxonomyError("UNKNOWN_TRAY_ACTION", $"Unknown tray action '{action}'."));
    }

    return TrayCommandResult(
        await TrayControlClient.SendAsync(new TrayControlRequest(command), cancellationToken),
        StatusCodes.Status202Accepted);
});

app.MapGet("/api/metadata/games", async (
    HttpContext context,
    MasterPlayContext db,
    SupabaseMetadataCatalog catalog,
    string? q,
    string? cursor,
    int? pageSize,
    CancellationToken cancellationToken) =>
{
    if (!TryGetLibraryOwnerKey(context, out var ownerKey))
        return AuthErrorResult(401, "AUTH_REQUIRED", "Sign in or continue as guest to search metadata.");

    var query = q?.Trim() ?? string.Empty;
    if (query.Length < 2 || query.Length > 120)
    {
        return Results.UnprocessableEntity(new
        {
            error = new { code = "METADATA_QUERY_INVALID", message = "Search text must contain 2-120 characters." }
        });
    }

    try
    {
        var size = Math.Clamp(pageSize ?? 24, 1, 100);
        var page = await catalog.SearchAsync(query, cursor, size, cancellationToken);
        var ids = page.Items.Select(item => item.Id).ToArray();
        var localRows = ids.Length == 0
            ? []
            : await db.Games.AsNoTracking()
                .Where(game => game.LibraryOwnerKey == ownerKey && game.MetadataGameId.HasValue && ids.Contains(game.MetadataGameId.Value))
                .Select(game => new { MetadataGameId = game.MetadataGameId!.Value, game.IsDisabled })
                .ToListAsync(cancellationToken);
        var localStatus = localRows.ToDictionary(
            row => row.MetadataGameId,
            row => row.IsDisabled ? "disabled" : "active");

        var items = page.Items
            .Select(item => ToMetadataGameCandidateDto(
                item,
                localStatus.TryGetValue(item.Id, out var status) ? status : "notInLibrary"))
            .ToList();

        return Results.Ok(new MetadataSearchResponseDto(items, page.NextCursor, size, page.HasMore, page.TotalItems));
    }
    catch (MetadataCatalogException exception)
    {
        return Results.Json(
            new { error = new { code = exception.Code, message = exception.Message } },
            statusCode: exception.StatusCode);
    }
});

app.MapPost("/api/library/game-imports", async (
    HttpContext context,
    MasterPlayContext db,
    SupabaseMetadataCatalog catalog,
    ImageAutoSyncQueue imageUploads,
    LibraryGameImportRequestDto body,
    CancellationToken cancellationToken) =>
{
    if (!TryGetLibraryOwnerKey(context, out var ownerKey))
        return AuthErrorResult(401, "AUTH_REQUIRED", "Sign in or continue as guest to import games.");

    if (body.Items is null || body.Items.Count == 0 || body.Items.Count > 100)
    {
        return Results.UnprocessableEntity(new
        {
            error = new { code = "IMPORT_ITEMS_INVALID", message = "Select between 1 and 100 games to import." }
        });
    }

    var requestedIds = body.Items.Select(item => item.MetadataGameId).ToArray();
    if (requestedIds.Any(id => id <= 0) || requestedIds.Length != requestedIds.Distinct().Count())
    {
        return Results.UnprocessableEntity(new
        {
            error = new { code = "IMPORT_IDS_INVALID", message = "Metadata game IDs must be positive and unique." }
        });
    }

    try
    {
        var metadata = await catalog.GetByIdsAsync(requestedIds, cancellationToken);
        var missing = requestedIds.Where(id => !metadata.ContainsKey(id)).ToArray();
        if (missing.Length > 0)
        {
            return Results.UnprocessableEntity(new
            {
                error = new
                {
                    code = "METADATA_GAME_NOT_FOUND",
                    message = "One or more selected metadata games no longer exist.",
                    details = new { metadataGameIds = missing }
                }
            });
        }

        var existing = await db.Games
            .Where(game => game.LibraryOwnerKey == ownerKey && game.MetadataGameId.HasValue && requestedIds.Contains(game.MetadataGameId.Value))
            .Select(game => new { game.MetadataGameId, game.Id, game.IsDisabled })
            .ToListAsync(cancellationToken);
        var existingByMetadata = existing.ToDictionary(row => row.MetadataGameId!.Value);

        var userDataErrors = new List<object>();
        foreach (var item in body.Items)
        {
            if (item.UserData is not null)
            {
                var error = await ValidateUserDataAsync(db, ToUserDataPatch(item.UserData), cancellationToken);
                if (error is not null)
                    userDataErrors.Add(new { metadataGameId = item.MetadataGameId, message = error });
            }
        }
        if (userDataErrors.Count > 0)
        {
            return Results.UnprocessableEntity(new
            {
                error = new { code = "IMPORT_USERDATA_INVALID", message = "One or more userdata values are invalid.", details = userDataErrors }
            });
        }

        var results = new List<LibraryGameImportResultItemDto>(body.Items.Count);
        var newItems = body.Items.Where(item => !existingByMetadata.ContainsKey(item.MetadataGameId)).ToList();
        var needsDefaultProgress = newItems.Any(item => item.UserData?.ProgressId is null);
        var defaultProgressId = needsDefaultProgress
            ? await FindProgressId(db, "Pending", cancellationToken)
            : null;
        var needsDefaultOwnership = newItems.Any(item => item.UserData?.OwnershipId is null);
        var defaultOwnershipId = needsDefaultOwnership
            ? await FindOwnershipId(db, "Wishlist", cancellationToken)
            : null;
        if (needsDefaultProgress && defaultProgressId is null)
        {
            return Results.Json(new
            {
                error = new
                {
                    code = "PENDING_PROGRESS_MISSING",
                    message = "The Pending progress catalog entry is unavailable."
                }
            }, statusCode: StatusCodes.Status409Conflict);
        }
        if (needsDefaultOwnership && defaultOwnershipId is null)
        {
            return Results.Json(new
            {
                error = new
                {
                    code = "WISHLIST_OWNERSHIP_MISSING",
                    message = "The Wishlist ownership catalog entry is unavailable."
                }
            }, statusCode: StatusCodes.Status409Conflict);
        }
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        foreach (var item in body.Items)
        {
            if (existingByMetadata.TryGetValue(item.MetadataGameId, out var current))
            {
                results.Add(new LibraryGameImportResultItemDto(
                    item.MetadataGameId,
                    current.IsDisabled ? "restoreRequired" : "alreadyInLibrary",
                    current.Id,
                    current.IsDisabled ? "Restore this game before importing it again." : "Game is already in your library."));
            }
        }

        var importedGames = new List<Game>(newItems.Count);
        foreach (var item in newItems)
        {
            var source = metadata[item.MetadataGameId];
            var game = new Game
            {
                Id = Guid.NewGuid().ToString(),
                LibraryOwnerKey = ownerKey,
                Name = source.Name,
                Developer = source.Developer,
                Publisher = source.Publisher,
                PublishDate = source.PublishDate,
                IsDlc = source.IsDlc,
                // Ba vai tro anh — DOMAIN_RULES §12. Doi ung MOT-MOT voi cot
                // cung ten tren kho; lech mot cap o day la anh card se hoa anh
                // hero sau moi vong import.
                BannerPath = source.BannerUrl,
                BackdropPath = source.BackdropUrl,
                PosterPath = source.PosterUrl,
                MetaScore = source.MetaScore,
                AvgPlayTime = source.AvgPlaytimeHours,
                CreateAt = DateTime.Now,
            };

            // Noi vao kho VA ghi moc so sanh. Ghi ngay tu luc import thi lan
            // `metadata-refresh` dau tien da co du ba gia tri de doi chieu, khong
            // phai di duong bootstrap "chi dien o trong".
            LibraryMetadataMerge.Record(game, source, DateTime.UtcNow);

            if (item.UserData is not null)
                ApplyUserData(game, ToUserDataPatch(item.UserData), db);
            game.ProgressId ??= defaultProgressId;
            game.OwnershipId ??= defaultOwnershipId;

            db.Games.Add(game);
            importedGames.Add(game);
            results.Add(new LibraryGameImportResultItemDto(item.MetadataGameId, "imported", game.Id, null));
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        // Anh tu kho la link goc (Steam/RAWG/IGDB) — dua len Cloudinary o nen.
        imageUploads.EnqueueIfNeeded(importedGames);

        return Results.Json(
            new LibraryGameImportResponseDto(
                results,
                results.Count(result => result.Status == "imported"),
                results.Count(result => result.Status == "alreadyInLibrary"),
                results.Count(result => result.Status == "restoreRequired")),
            statusCode: StatusCodes.Status201Created);
    }
    catch (MetadataCatalogException exception)
    {
        return Results.Json(
            new { error = new { code = exception.Code, message = exception.Message } },
            statusCode: exception.StatusCode);
    }
});

/*
 * Keo thay doi tu kho ve thu vien.
 *
 * Admin sua mot ban ghi tren kho thi moi thu vien da noi toi ban ghi do phai
 * thay doi theo, TRU field ma chu thu vien da sua tay: sua tay thang. Luat hop
 * nhat ba chieu nam trong `LibraryMetadataMerge`, cung noi giai thich tai sao
 * phai co baseline.
 *
 * KHONG dat o `GET /api/games`: mot GET ma ghi DB va goi mang la mot cai bay —
 * moi lan doi trang thu vien lai tra them mot round-trip, va loi mang se lam
 * hong ca man hinh danh sach. Client goi endpoint nay mot lan moi phien.
 *
 * Co lay ca dong `IsDisabled`: nguoi dung khoi phuc mot game thi no phai san
 * sang dung, chu khong phai cho them mot vong dong bo nua.
 */
app.MapPost("/api/library/metadata-refresh", async (
    HttpContext context,
    MasterPlayContext db,
    SupabaseMetadataCatalog catalog,
    ImageAutoSyncQueue imageUploads,
    LibraryMetadataRefreshRequestDto? body,
    CancellationToken cancellationToken) =>
{
    if (!TryGetLibraryOwnerKey(context, out var ownerKey))
        return AuthErrorResult(401, "AUTH_REQUIRED", "Sign in or continue as guest to refresh metadata.");

    var dryRun = body?.DryRun is true;
    var force = body?.Force is true;

    var games = await db.Games
        .Where(game => game.LibraryOwnerKey == ownerKey && game.MetadataGameId != null)
        .ToListAsync(cancellationToken);

    if (games.Count == 0)
        return Results.Ok(LibraryMetadataRefreshResponseDto.Nothing(dryRun));

    // Cua chan: client goi moi lan mo app, va mo lai app 20 lan trong mot buoi
    // khong co nghia la hoi kho 20 lan. `force` danh cho nut bam tay, khi nguoi
    // dung vua sua ben console va muon thay ket qua ngay.
    var floor = DateTime.UtcNow - TimeSpan.FromMinutes(10);
    if (!force && games.All(game => game.MetadataSyncedAt is { } at && at > floor))
    {
        return Results.Ok(LibraryMetadataRefreshResponseDto.Nothing(dryRun) with
        {
            Skipped = true,
            Linked = games.Count,
        });
    }

    try
    {
        var ids = games.Select(game => game.MetadataGameId!.Value).Distinct().ToArray();
        var metadata = new Dictionary<long, MetadataCatalogGame>();
        // `GetByIdsAsync` dung `id=in.(...)` tren URL. Cat lo de URL khong dai vo han.
        foreach (var chunk in ids.Chunk(100))
        {
            foreach (var pair in await catalog.GetByIdsAsync(chunk, cancellationToken))
                metadata[pair.Key] = pair.Value;
        }

        var syncedAt = DateTime.UtcNow;
        var reports = new List<LibraryMetadataRefreshGameDto>();
        var missing = 0;
        var fieldsUpdated = 0;
        var fieldsKept = 0;
        var imageChanged = new List<Game>();

        foreach (var game in games)
        {
            // Kho khong tra ve: ban ghi bi go xuat ban hoac da xoa. KHONG dung
            // toi dong thu vien — game van la cua nguoi dung, chi la kho khong
            // con noi gi ve no nua.
            if (!metadata.TryGetValue(game.MetadataGameId!.Value, out var source))
            {
                missing++;
                continue;
            }

            var result = LibraryMetadataMerge.Merge(game, source, syncedAt, apply: !dryRun);
            if (result.Updated.Any(LibraryMetadataMerge.IsImageField))
                imageChanged.Add(game);
            fieldsUpdated += result.Updated.Count;
            fieldsKept += result.Kept.Count;
            if (result.Updated.Count > 0 || result.Kept.Count > 0)
            {
                reports.Add(new LibraryMetadataRefreshGameDto(
                    result.GameId, result.Name, result.MetadataGameId, result.Updated, result.Kept));
            }
        }

        if (!dryRun)
        {
            await db.SaveChangesAsync(cancellationToken);
            // Kho doi anh thi thu vien vua nhan link goc moi — dua len Cloudinary o nen.
            imageUploads.EnqueueIfNeeded(imageChanged);
        }

        return Results.Ok(new LibraryMetadataRefreshResponseDto(
            DryRun: dryRun,
            Skipped: false,
            Linked: games.Count,
            Checked: games.Count - missing,
            Updated: reports.Count(report => report.Updated.Count > 0),
            FieldsUpdated: fieldsUpdated,
            FieldsKept: fieldsKept,
            Missing: missing,
            Games: reports));
    }
    catch (MetadataCatalogException exception)
    {
        return MetadataErrorResult(exception);
    }
});

app.MapPatch("/api/games/{id}/userdata", async (
    HttpContext context,
    MasterPlayContext db,
    string id,
    UserDataPatchDto body,
    CancellationToken cancellationToken) =>
{
    if (!TryGetLibraryOwnerKey(context, out var ownerKey))
        return AuthErrorResult(401, "AUTH_REQUIRED", "Sign in or continue as guest to update userdata.");

    var error = await ValidateUserDataAsync(db, body, cancellationToken);
    if (error is not null)
    {
        return Results.UnprocessableEntity(new
        {
            error = new { code = "USERDATA_INVALID", message = error }
        });
    }

    var game = await db.Games
        .Include(item => item.OwnershipInfo)
        .SingleOrDefaultAsync(item => item.Id == id && item.LibraryOwnerKey == ownerKey && !item.IsDisabled, cancellationToken);
    if (game is null)
    {
        return Results.NotFound(new
        {
            error = new { code = "GAME_NOT_FOUND", message = $"Game not found: {id}" }
        });
    }

    ApplyUserData(game, body, db);
    await db.SaveChangesAsync(cancellationToken);
    return Results.Ok(await ReloadGameDetailDto(db, id, ownerKey));
});

// ── Admin Console — kho metadata dung chung ────────────────────
// Spec 2026-09-14 §7. Moi endpoint tu kiem role tu JWT: mode cua UI khong duoc
// tham gia vao quyet dinh quyen.
//
// KHONG co endpoint nao cho metadata genres/themes — hai bang do thuoc tien trinh
// sync (§4.4). Dung them "cho tien": co duong ghi la se co nguoi sua tay, roi lan
// sync sau ghi de.

app.MapGet("/api/admin/metadata/series", async (
    HttpContext context,
    SupabaseMetadataAdmin admin,
    string? q,
    int? limit,
    CancellationToken cancellationToken) =>
{
    if (RequireAdmin(context, out _) is { } denied) return denied;

    try
    {
        var items = await admin.ListSeriesAsync(q, limit ?? 100, cancellationToken);
        return Results.Ok(items.Select(item => new AdminMetadataSeriesDto(item.Id, item.Name)).ToList());
    }
    catch (MetadataCatalogException exception)
    {
        return MetadataErrorResult(exception);
    }
});

app.MapPost("/api/admin/metadata/series", async (
    HttpContext context,
    SupabaseMetadataAdmin admin,
    AdminMetadataSeriesWriteDto body,
    CancellationToken cancellationToken) =>
{
    if (RequireAdmin(context, out var account) is { } denied) return denied;
    if (ValidateSeriesName(body?.Name) is { } invalid) return invalid;

    try
    {
        var created = await admin.CreateSeriesAsync(body!.Name!.Trim(), account.Id, cancellationToken);
        return Results.Created(
            $"/api/admin/metadata/series/{created.Id}",
            new AdminMetadataSeriesDto(created.Id, created.Name));
    }
    catch (MetadataCatalogException exception)
    {
        return MetadataErrorResult(exception);
    }
});

app.MapPut("/api/admin/metadata/series/{id:long}", async (
    HttpContext context,
    SupabaseMetadataAdmin admin,
    long id,
    AdminMetadataSeriesWriteDto body,
    CancellationToken cancellationToken) =>
{
    if (RequireAdmin(context, out var account) is { } denied) return denied;
    if (ValidateSeriesName(body?.Name) is { } invalid) return invalid;

    try
    {
        var saved = await admin.UpdateSeriesAsync(id, body!.Name!.Trim(), account.Id, cancellationToken);
        return Results.Ok(new AdminMetadataSeriesDto(saved.Id, saved.Name));
    }
    catch (MetadataCatalogException exception)
    {
        return MetadataErrorResult(exception);
    }
});

app.MapDelete("/api/admin/metadata/series/{id:long}", async (
    HttpContext context,
    SupabaseMetadataAdmin admin,
    long id,
    CancellationToken cancellationToken) =>
{
    if (RequireAdmin(context, out var account) is { } denied) return denied;

    try
    {
        await admin.DeleteSeriesAsync(id, account.Id, cancellationToken);
        return Results.NoContent();
    }
    catch (MetadataCatalogException exception)
    {
        return MetadataErrorResult(exception);
    }
});

app.MapPost("/api/admin/metadata/series/merge", async (
    HttpContext context,
    SupabaseMetadataAdmin admin,
    AdminMetadataSeriesMergeDto body,
    CancellationToken cancellationToken) =>
{
    if (RequireAdmin(context, out var account) is { } denied) return denied;

    if (body?.SourceId is not > 0 || body.TargetId is not > 0)
    {
        return Results.UnprocessableEntity(new
        {
            error = new { code = "SERIES_MERGE_IDS_INVALID", message = "Choose a source and a target series." }
        });
    }

    try
    {
        var result = await admin.MergeSeriesAsync(body.SourceId.Value, body.TargetId.Value, account.Id, cancellationToken);
        return Results.Ok(new AdminMetadataSeriesMergeResponseDto(
            new AdminMetadataSeriesDto(result.Target.Id, result.Target.Name),
            result.MovedGames));
    }
    catch (MetadataCatalogException exception)
    {
        return MetadataErrorResult(exception);
    }
});

app.MapGet("/api/admin/metadata/stats", async (
    HttpContext context,
    SupabaseMetadataAdmin admin,
    CancellationToken cancellationToken) =>
{
    if (RequireAdmin(context, out _) is { } denied) return denied;

    try
    {
        return Results.Ok(await admin.GetStatsAsync(cancellationToken));
    }
    catch (MetadataCatalogException exception)
    {
        return MetadataErrorResult(exception);
    }
});

app.MapGet("/api/admin/metadata/crawler", async (
    HttpContext context,
    SupabaseMetadataAdmin admin,
    CancellationToken cancellationToken) =>
{
    if (RequireAdmin(context, out _) is { } denied) return denied;
    try { return Results.Ok(await admin.GetCrawlerStatusAsync(cancellationToken)); }
    catch (MetadataCatalogException exception) { return MetadataErrorResult(exception); }
});

app.MapPut("/api/admin/metadata/crawler/enabled", async (
    HttpContext context,
    SupabaseMetadataAdmin admin,
    AdminCrawlerEnabledRequestDto? body,
    CancellationToken cancellationToken) =>
{
    if (RequireAdmin(context, out _) is { } denied) return denied;
    if (body?.Enabled is null)
        return MetadataParamError("CRAWLER_ENABLED_REQUIRED", "Specify whether the crawler should be enabled.");
    try { return Results.Ok(await admin.SetCrawlerEnabledAsync(body.Enabled.Value, cancellationToken)); }
    catch (MetadataCatalogException exception) { return MetadataErrorResult(exception); }
});

app.MapPost("/api/admin/metadata/crawler/run", async (
    HttpContext context,
    SupabaseMetadataAdmin admin,
    CancellationToken cancellationToken) =>
{
    if (RequireAdmin(context, out _) is { } denied) return denied;
    try
    {
        var queued = await admin.TriggerCrawlerAsync(cancellationToken);
        return Results.Json(queued, statusCode: StatusCodes.Status202Accepted);
    }
    catch (MetadataCatalogException exception) { return MetadataErrorResult(exception); }
});

app.MapGet("/api/admin/metadata/crawler/runs", async (
    HttpContext context,
    SupabaseMetadataAdmin admin,
    string? cursor,
    int? pageSize,
    CancellationToken cancellationToken) =>
{
    if (RequireAdmin(context, out _) is { } denied) return denied;
    if (!TryParseMetadataCursor(cursor, out var parsedCursor))
        return MetadataParamError("CRAWLER_CURSOR_INVALID", "That page cursor is not valid. Reload the list.");
    try { return Results.Ok(await admin.ListCrawlerRunsAsync(parsedCursor, Math.Clamp(pageSize ?? 25, 1, 100), cancellationToken)); }
    catch (MetadataCatalogException exception) { return MetadataErrorResult(exception); }
});

app.MapGet("/api/admin/metadata/crawler/runs/{runId:guid}/games", async (
    HttpContext context,
    SupabaseMetadataAdmin admin,
    Guid runId,
    string? cursor,
    int? pageSize,
    CancellationToken cancellationToken) =>
{
    if (RequireAdmin(context, out _) is { } denied) return denied;
    if (!TryParseMetadataCursor(cursor, out var parsedCursor))
        return MetadataParamError("CRAWLER_CURSOR_INVALID", "That page cursor is not valid. Reload the list.");
    try { return Results.Ok(await admin.ListCrawlerRunItemsAsync(runId, parsedCursor, Math.Clamp(pageSize ?? 30, 1, 100), cancellationToken)); }
    catch (MetadataCatalogException exception) { return MetadataErrorResult(exception); }
});

app.MapGet("/api/admin/metadata/crawler/games", async (
    HttpContext context,
    SupabaseMetadataAdmin admin,
    string? cursor,
    int? pageSize,
    CancellationToken cancellationToken) =>
{
    if (RequireAdmin(context, out _) is { } denied) return denied;
    if (!TryParseMetadataCursor(cursor, out var parsedCursor))
        return MetadataParamError("CRAWLER_CURSOR_INVALID", "That page cursor is not valid. Reload the list.");
    try { return Results.Ok(await admin.ListCrawlerGamesAsync(parsedCursor, Math.Clamp(pageSize ?? 30, 1, 100), cancellationToken)); }
    catch (MetadataCatalogException exception) { return MetadataErrorResult(exception); }
});

app.MapGet("/api/admin/metadata/gemini-exe", async (HttpContext context, SupabaseMetadataAdmin admin, CancellationToken cancellationToken) =>
{
    if (RequireAdmin(context, out _) is { } denied) return denied;
    try { return Results.Ok(await admin.GetGeminiExeStatusAsync(cancellationToken)); }
    catch (MetadataCatalogException exception) { return MetadataErrorResult(exception); }
});

app.MapPut("/api/admin/metadata/gemini-exe/enabled", async (HttpContext context, SupabaseMetadataAdmin admin, AdminCrawlerEnabledRequestDto? body, CancellationToken cancellationToken) =>
{
    if (RequireAdmin(context, out _) is { } denied) return denied;
    if (body?.Enabled is null) return MetadataParamError("GEMINI_EXE_ENABLED_REQUIRED", "Specify whether Gemini EXE enrichment should be enabled.");
    try { return Results.Ok(await admin.SetGeminiExeEnabledAsync(body.Enabled.Value, cancellationToken)); }
    catch (MetadataCatalogException exception) { return MetadataErrorResult(exception); }
});

app.MapPost("/api/admin/metadata/gemini-exe/run", async (HttpContext context, SupabaseMetadataAdmin admin, CancellationToken cancellationToken) =>
{
    if (RequireAdmin(context, out _) is { } denied) return denied;
    try { return Results.Json(await admin.TriggerGeminiExeAsync(cancellationToken), statusCode: StatusCodes.Status202Accepted); }
    catch (MetadataCatalogException exception) { return MetadataErrorResult(exception); }
});

app.MapGet("/api/admin/metadata/gemini-exe/runs", async (HttpContext context, SupabaseMetadataAdmin admin, int? pageSize, CancellationToken cancellationToken) =>
{
    if (RequireAdmin(context, out _) is { } denied) return denied;
    try { return Results.Ok(await admin.ListGeminiExeRunsAsync(Math.Clamp(pageSize ?? 25, 1, 100), cancellationToken)); }
    catch (MetadataCatalogException exception) { return MetadataErrorResult(exception); }
});

app.MapGet("/api/admin/metadata/gemini-exe/games", async (HttpContext context, SupabaseMetadataAdmin admin, int? pageSize, CancellationToken cancellationToken) =>
{
    if (RequireAdmin(context, out _) is { } denied) return denied;
    try { return Results.Ok(await admin.ListGeminiExeGamesAsync(Math.Clamp(pageSize ?? 30, 1, 100), cancellationToken)); }
    catch (MetadataCatalogException exception) { return MetadataErrorResult(exception); }
});

app.MapGet("/api/admin/metadata/games", async (
    HttpContext context,
    SupabaseMetadataAdmin admin,
    string? q,
    string? missing,
    string? sort,
    bool? desc,
    bool? isDlc,
    long? seriesId,
    bool? noSeries,
    int? year,
    bool? published,
    string? cursor,
    int? pageSize,
    int? page,
    CancellationToken cancellationToken) =>
{
    if (RequireAdmin(context, out _) is { } denied) return denied;

    if (page is < 1)
        return MetadataParamError("METADATA_PAGE_INVALID", "Page number must be 1 or greater.");

    // Gia tri la bi tu choi TAI DAY thay vi de DB tra loi: loi 422 cua Host noi
    // ro tham so nao sai, con loi tu DB chi noi "filter khong ho tro".
    if (!string.IsNullOrWhiteSpace(missing) && !SupabaseMetadataAdmin.IsKnownMissingFilter(missing))
        return MetadataParamError("METADATA_FILTER_UNKNOWN", "That catalog filter is not supported.");

    if (!string.IsNullOrWhiteSpace(sort) && !SupabaseMetadataAdmin.IsKnownSortKey(sort))
        return MetadataParamError("METADATA_SORT_UNKNOWN", "That sort key is not supported.");

    if (year is < 1950 or > 2200)
        return MetadataParamError("METADATA_YEAR_INVALID", "Release year must be between 1950 and 2200.");

    /*
     * Con tro den tu client duoi dang chuoi JSON do chinh DB phat ra o trang
     * truoc. Host KHONG doc noi dung, chi kiem tra no la JSON hop le — parse duoc
     * thi chuyen tiep, khong thi bao loi. Nhoi chuoi hong xuong DB se thanh mot
     * loi 503 kho hieu thay vi mot loi 422 dung cho.
     */
    JsonElement? parsedCursor = null;
    if (!string.IsNullOrWhiteSpace(cursor))
    {
        try
        {
            parsedCursor = JsonDocument.Parse(cursor).RootElement.Clone();
        }
        catch (JsonException)
        {
            return MetadataParamError("METADATA_CURSOR_INVALID", "That page cursor is not valid. Reload the list.");
        }
    }

    try
    {
        var size = Math.Clamp(pageSize ?? 30, 1, 100);
        var result = await admin.ListGamesAsync(
            new MetadataAdminGameQuery
            {
                Text = q,
                Missing = missing,
                IsDlc = isDlc,
                SeriesId = seriesId,
                NoSeries = noSeries,
                Year = year,
                Published = published,
                Sort = string.IsNullOrWhiteSpace(sort) ? "id" : sort,
                Descending = desc ?? true,
                Cursor = parsedCursor,
                // `page` la so trang (tu 1) cua che do danh sach; con tro thang
                // neu client gui ca hai.
                Offset = page is { } number && parsedCursor is null ? (number - 1) * size : null,
                PageSize = size,
            },
            cancellationToken);

        return Results.Ok(new AdminMetadataGameListResponseDto(
            result.Items.Select(ToAdminMetadataGameDto).ToList(),
            // Con tro di nguoc ra client duoi dang chuoi, de no chi viec gui lai
            // nguyen si mà khong phai hieu cau truc ben trong.
            result.NextCursor?.GetRawText(),
            size,
            result.TotalItems));
    }
    catch (MetadataCatalogException exception)
    {
        return MetadataErrorResult(exception);
    }
});

app.MapGet("/api/admin/metadata/games/{id:long}", async (
    HttpContext context,
    SupabaseMetadataAdmin admin,
    long id,
    CancellationToken cancellationToken) =>
{
    if (RequireAdmin(context, out _) is { } denied) return denied;

    try
    {
        var game = await admin.GetGameAsync(id, cancellationToken);
        return game is null
            ? Results.NotFound(new
            {
                error = new { code = "GAME_NOT_FOUND", message = "That metadata record no longer exists." }
            })
            : Results.Ok(ToAdminMetadataGameDto(game));
    }
    catch (MetadataCatalogException exception)
    {
        return MetadataErrorResult(exception);
    }
});

app.MapPost("/api/admin/metadata/games", async (
    HttpContext context,
    SupabaseMetadataAdmin admin,
    AdminMetadataGameWriteDto body,
    CancellationToken cancellationToken) =>
{
    if (RequireAdmin(context, out var account) is { } denied) return denied;
    if (BuildMetadataGamePayload(body, out var payload) is { } invalid) return invalid;

    try
    {
        var created = await admin.CreateGameAsync(
            payload,
            account.Id,
            body!.ConfirmDuplicate == true,
            cancellationToken);
        return Results.Created(
            $"/api/admin/metadata/games/{created.Id}",
            ToAdminMetadataGameDto(created));
    }
    catch (MetadataCatalogException exception)
    {
        return MetadataErrorResult(exception);
    }
});

app.MapPut("/api/admin/metadata/games/{id:long}", async (
    HttpContext context,
    SupabaseMetadataAdmin admin,
    long id,
    AdminMetadataGameWriteDto body,
    CancellationToken cancellationToken) =>
{
    if (RequireAdmin(context, out var account) is { } denied) return denied;

    // Khong co `revision` thi khong cho ghi. Cho qua o day dong nghia voi
    // last-write-wins tren kho dung chung — dung thu spec §7 cam.
    if (body?.Revision is not > 0)
    {
        return Results.UnprocessableEntity(new
        {
            error = new
            {
                code = "METADATA_REVISION_REQUIRED",
                message = "Send the revision you are editing so concurrent changes can be detected."
            }
        });
    }

    if (BuildMetadataGamePayload(body, out var payload) is { } invalid) return invalid;

    try
    {
        var saved = await admin.UpdateGameAsync(id, payload, body.Revision.Value, account.Id, cancellationToken);
        return Results.Ok(ToAdminMetadataGameDto(saved));
    }
    catch (MetadataCatalogException exception)
    {
        return MetadataErrorResult(exception);
    }
});

app.MapDelete("/api/admin/metadata/games/{id:long}", async (
    HttpContext context,
    SupabaseMetadataAdmin admin,
    long id,
    CancellationToken cancellationToken) =>
{
    if (RequireAdmin(context, out var account) is { } denied) return denied;

    try
    {
        var result = await admin.DeleteGameAsync(id, account.Id, cancellationToken);
        return Results.Ok(new AdminMetadataGameDeleteResponseDto(result.OrphanedDlcCount));
    }
    catch (MetadataCatalogException exception)
    {
        return MetadataErrorResult(exception);
    }
});

/*
 * TIEU CHI XUAT BAN — spec §13.
 *
 * `manually_unpublished` la Y DINH cua admin; `is_published` la KET QUA suy ra, do
 * trigger DB giu. Host khong bao gio ghi `is_published`.
 *
 * MAC DINH la DA XUAT BAN: ban ghi chi khong len duoc khi admin chu dong go, hoac
 * khi no truot mot tieu chi dang bat.
 */
app.MapGet("/api/admin/metadata/publishing", async (
    HttpContext context,
    SupabaseMetadataAdmin admin,
    CancellationToken cancellationToken) =>
{
    if (RequireAdmin(context, out _) is { } denied) return denied;

    try
    {
        return Results.Ok(await admin.GetPublishRulesAsync(cancellationToken));
    }
    catch (MetadataCatalogException exception)
    {
        return MetadataErrorResult(exception);
    }
});

app.MapPut("/api/admin/metadata/publishing", async (
    HttpContext context,
    SupabaseMetadataAdmin admin,
    AdminPublishRulesDto? body,
    CancellationToken cancellationToken) =>
{
    if (RequireAdmin(context, out var account) is { } denied) return denied;
    if (body is null)
        return MetadataParamError("PUBLISH_RULES_REQUIRED", "Send at least one rule to change.");

    // Field bo trong = GIU NGUYEN. Chi gui len nhung khoa that su co gia tri, de
    // ham SQL phan biet duoc "khong doi" voi "tat di".
    var rules = new Dictionary<string, object?>();
    void Put(string key, bool? value) { if (value.HasValue) rules[key] = value.Value; }
    Put("requireBanner", body.RequireBanner);
    Put("requirePoster", body.RequirePoster);
    Put("requireBackdrop", body.RequireBackdrop);
    Put("requireDeveloper", body.RequireDeveloper);
    Put("requirePublisher", body.RequirePublisher);
    Put("requirePublishDate", body.RequirePublishDate);
    Put("requireMetaScore", body.RequireMetaScore);
    Put("requireAvgPlaytime", body.RequireAvgPlaytime);
    Put("requireSummary", body.RequireSummary);
    Put("requireIgdbId", body.RequireIgdbId);
    Put("requireDlcBase", body.RequireDlcBase);

    if (rules.Count == 0)
        return MetadataParamError("PUBLISH_RULES_REQUIRED", "Send at least one rule to change.");

    try
    {
        return Results.Ok(await admin.SetPublishRulesAsync(rules, account.Id, cancellationToken));
    }
    catch (MetadataCatalogException exception)
    {
        return MetadataErrorResult(exception);
    }
});

/*
 * Xuat ban / go xuat ban thu cong. KHONG nhan `revision`: thao tac nay khong doi
 * noi dung ban ghi nen no khong duoc dinh 409 vi mot ban sua do dang mo o cho khac.
 *
 * Ban ghi dang truot tieu chi VAN nhan duoc `published: true` — do la y dinh hop
 * le cua admin, chi la no chua co hieu luc. Chan o day se khien admin khong bao
 * gio go duoc y dinh cu sau khi bo sung du lieu.
 */
app.MapPut("/api/admin/metadata/games/{id:long}/published", async (
    HttpContext context,
    SupabaseMetadataAdmin admin,
    long id,
    AdminMetadataGamePublishedRequestDto? body,
    CancellationToken cancellationToken) =>
{
    if (RequireAdmin(context, out var account) is { } denied) return denied;
    if (body?.Published is not { } published)
        return MetadataParamError("PUBLISHED_REQUIRED", "Specify whether the record should be published.");

    try
    {
        var game = await admin.SetGamePublishedAsync(id, published, account.Id, cancellationToken);
        return Results.Ok(ToAdminMetadataGameDto(game));
    }
    catch (MetadataCatalogException exception)
    {
        return MetadataErrorResult(exception);
    }
});

// ── Admin Integrations ─────────────────────────────────────────────────────
// Credentials are write-only at this HTTP boundary. The response is a safe
// configuration summary sourced from Supabase Vault metadata plus the provider's
// public identifiers (Cloudinary cloud name, IGDB client ID) — never a secret.
app.MapGet("/api/admin/integrations", async (
    HttpContext context,
    SupabaseIntegrationStore integrations,
    CancellationToken cancellationToken) =>
{
    if (RequireAdmin(context, out _) is { } denied) return denied;
    try
    {
        var summaries = new List<IntegrationSummaryDto>();
        foreach (var summary in await integrations.ListAsync(cancellationToken))
            summaries.Add(await integrations.WithIdentifiersAsync(summary, cancellationToken));
        return Results.Ok(summaries);
    }
    catch (IntegrationStoreException exception)
    {
        return IntegrationErrorResult(exception);
    }
});

app.MapPut("/api/admin/integrations/{provider}", async (
    HttpContext context,
    SupabaseIntegrationStore integrations,
    string provider,
    IntegrationCredentialsWriteDto? body,
    CancellationToken cancellationToken) =>
{
    if (RequireAdmin(context, out var account) is { } denied) return denied;
    if (!IntegrationProviders.IsKnown(provider))
        return IntegrationParamError("INTEGRATION_PROVIDER_UNKNOWN", "That integration is not supported.");
    if (body?.Values is null || body.Values.Count is < 1 or > 3 ||
        body.Values.Any(value => value.Key.Length is < 1 or > 40 || value.Value is null || value.Value.Length is < 1 or > 4096))
    {
        return IntegrationParamError("INTEGRATION_FIELDS_REQUIRED", "Provide the complete credential set for this integration.");
    }

    try
    {
        var summary = await integrations.ReplaceAsync(provider, body.Values, account.Id, cancellationToken);
        return Results.Ok(await integrations.WithIdentifiersAsync(summary, cancellationToken));
    }
    catch (IntegrationStoreException exception)
    {
        return IntegrationErrorResult(exception);
    }
});

app.MapPost("/api/admin/integrations/{provider}/test", async (
    HttpContext context,
    SupabaseIntegrationStore integrations,
    IntegrationConnectionTester tester,
    string provider,
    CancellationToken cancellationToken) =>
{
    if (RequireAdmin(context, out var account) is { } denied) return denied;
    if (!IntegrationProviders.IsKnown(provider))
        return IntegrationParamError("INTEGRATION_PROVIDER_UNKNOWN", "That integration is not supported.");

    try
    {
        var result = await tester.TestAsync(provider, cancellationToken);
        var summary = await integrations.RecordTestAsync(
            provider,
            result.Passed,
            result.ErrorCode,
            account.Id,
            cancellationToken);
        return Results.Ok(await integrations.WithIdentifiersAsync(summary, cancellationToken));
    }
    catch (IntegrationStoreException exception)
    {
        return IntegrationErrorResult(exception);
    }
}).RequireRateLimiting("integration-test");

app.MapGet("/api/settings", () => Results.Ok(ToSettingsDto(JsonService.LoadConfig())));

app.MapPut("/api/settings", (UpdateSettingsDto body) =>
{
    if (body.ThemeMode is not null && body.ThemeMode is not ("light" or "dark" or "system" or "jungle"))
    {
        return Results.BadRequest(TaxonomyError(
            "INVALID_THEME_MODE",
            "themeMode must be light, dark, system, or jungle."));
    }

    if (body.JungleFx is not null && body.JungleFx is not ("auto" or "off" or "medium" or "full"))
    {
        return Results.BadRequest(TaxonomyError(
            "INVALID_JUNGLE_FX",
            "jungleFx must be auto, off, medium, or full."));
    }

    if (body.JungleWeather is not null && body.JungleWeather is not ("clear" or "rain"))
    {
        return Results.BadRequest(TaxonomyError(
            "INVALID_JUNGLE_WEATHER",
            "jungleWeather must be clear or rain."));
    }

    if (body.AppMode is not null && body.AppMode is not ("desktop" or "console"))
    {
        return Results.BadRequest(TaxonomyError(
            "INVALID_APP_MODE",
            "appMode must be desktop or console."));
    }

    if (body.SeenGuides is not null && body.SeenGuides.Any(guide => guide is not ("gameForm" or "viewModes")))
    {
        return Results.BadRequest(TaxonomyError(
            "INVALID_SEEN_GUIDES",
            "seenGuides may only contain gameForm or viewModes."));
    }

    if (body.Language is not null && body.Language is not ("en-US" or "vi-VN"))
    {
        return Results.BadRequest(TaxonomyError(
            "INVALID_LANGUAGE",
            "language must be en-US or vi-VN."));
    }

    var config = JsonService.LoadConfig();
    if (body.ThemeMode is not null) config.ThemeMode = body.ThemeMode;
    if (body.JungleFx is not null) config.JungleFx = body.JungleFx;
    if (body.JungleWeather is not null) config.JungleWeather = body.JungleWeather;
    if (body.AppMode is not null) config.AppMode = body.AppMode;
    if (body.SeenGuides is not null) config.SeenGuides = body.SeenGuides.Distinct().ToList();
    if (body.Language is not null) config.Language = body.Language;
    if (body.BackupRootPath is not null) config.BackupRootPath = body.BackupRootPath.Trim();
    if (body.DatabasePath is not null) config.DatabasePath = body.DatabasePath.Trim();
    if (body.Libraries?.Paths is not null)
    {
        config.GameLibraryPath = body.Libraries.Paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
    if (body.Cloudinary?.CloudName is not null)
        config.CloudinaryCloudName = body.Cloudinary.CloudName.Trim();

    JsonService.SaveConfig(config);
    return Results.Ok(ToSettingsDto(config));
});

app.MapGet("/api/library/sort-profiles", () =>
    Results.Ok(LibrarySortProfiles.GetAll()));

app.MapPut("/api/library/sort-profiles/{view}", (
    string view,
    UpdateViewSortProfileDto body) =>
{
    if (!LibrarySortProfiles.TryValidate(view, body.Rules, out var code, out var message))
    {
        return Results.BadRequest(new { error = new { code, message } });
    }

    return Results.Ok(LibrarySortProfiles.Save(view, body.Rules!));
});

app.MapDelete("/api/library/sort-profiles/{view}", (string view) =>
{
    if (!LibrarySortProfiles.IsConfigurableView(view))
    {
        return Results.BadRequest(new
        {
            error = new
            {
                code = "INVALID_SORT_PROFILE_VIEW",
                message = "Sort profiles are supported only for all, planning, and finished."
            }
        });
    }

    return Results.Ok(LibrarySortProfiles.Reset(view));
});

app.MapGet("/api/games", async (
    HttpContext context,
    MasterPlayContext db,
    string? tab,
    string? q,
    string? scope,
    string? sort,
    bool? reverseSort,
    int? page,
    int? pageSize,
    string? finishedYearOp,
    double? finishedYearValue,
    string? excitedOp,
    double? excitedValue,
    string? scoreOp,
    double? scoreValue,
    string? playtimeOp,
    int? playtimeSeconds,
    string? avgBands,
    bool? includeAvgNone,
    string? metaBands,
    bool? includeMetaNone,
    string? progressKeys,
    string? ownershipKeys,
    string? genreIds,
    string? themeIds,
    bool? reverseFilter) =>
{
    if (!TryGetLibraryOwnerKey(context, out var ownerKey))
        return AuthErrorResult(401, "AUTH_REQUIRED", "Sign in or continue as guest to view your library.");

    tab ??= "all";
    scope ??= "all";
    sort ??= "default";
    var pageNum = page is > 0 ? page.Value : 1;
    var size = pageSize is > 0 ? pageSize.Value : 30;

    static string[] Csv(string? s) =>
        string.IsNullOrWhiteSpace(s)
            ? []
            : s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    // Id sai dinh dang thi bao loi, KHONG bo qua am tham — bo qua se tra ket qua
    // nhu chua loc ma client tuong da loc.
    static int[]? CsvInts(string? s)
    {
        var parts = Csv(s);
        var ids = new int[parts.Length];
        for (var i = 0; i < parts.Length; i++)
            if (!int.TryParse(parts[i], out ids[i])) return null;
        return ids;
    }

    var genreIdList = CsvInts(genreIds);
    var themeIdList = CsvInts(themeIds);
    if (genreIdList is null || themeIdList is null)
        return Results.BadRequest(new { error = new { code = "BAD_FILTER", message = "genreIds/themeIds phai la danh sach so nguyen, cach nhau bang dau phay." } });

    var filter = new LibraryQuery.FilterArgs
    {
        FinishedYearOp = finishedYearOp,
        FinishedYearValue = finishedYearValue,
        ExcitedOp = excitedOp,
        ExcitedValue = excitedValue,
        ScoreOp = scoreOp,
        ScoreValue = scoreValue,
        PlaytimeOp = playtimeOp,
        PlaytimeSeconds = playtimeSeconds,
        AvgBands = Csv(avgBands),
        IncludeAvgNone = includeAvgNone == true,
        MetaBands = Csv(metaBands),
        IncludeMetaNone = includeMetaNone == true,
        ProgressKeys = Csv(progressKeys),
        OwnershipKeys = Csv(ownershipKeys),
        GenreIds = genreIdList,
        ThemeIds = themeIdList,
        ReverseFilter = reverseFilter == true,
    };

    // Chi lay game chua bi disable — DOMAIN_RULES §4.
    var games = await db.Games
        .AsNoTracking()
        .Where(g => g.LibraryOwnerKey == ownerKey && !g.IsDisabled)
        .Include(g => g.Progress)
        .Include(g => g.Ownership)
        .Include(g => g.Series)
        // Dung bang noi TUONG MINH GameGenres/GameThemes — giong WPF.
        // KHONG dung skip-navigation g.Genres/g.Themes: EF suy ra bang "GameGenre"
        // (so it) khong ton tai trong DB that.
        .Include(g => g.GameGenres).ThenInclude(gg => gg.Genre)
        .Include(g => g.GameThemes).ThenInclude(gt => gt.Theme)
        .Include(g => g.Finishes)
        .Include(g => g.GamePosition)
        .ToListAsync();

    var launchTargets = await LaunchTargets.LoadAsync(db);
    IEnumerable<Game> result = LibraryQuery.ApplyTab(games, tab, launchTargets);
    if (tab == "queue")
    {
        // Queue owns its order and ignores library search/filter/sort controls.
        result = LibraryQuery.ApplySort(result, "default", "queue");
    }
    else if (tab == "console")
    {
        // Console is a fixed server-owned view. Hidden Gallery controls must not
        // leak stale search/filter/sort state into this presentation.
        result = LibraryQuery.ApplySort(result, "default", "console");
    }
    else
    {
        result = LibraryQuery.ApplySearch(result, q, scope);
        result = LibraryQuery.ApplyFilter(result, filter);
        result = sort == "default"
            ? LibraryQuery.ApplySortProfile(result, LibrarySortProfiles.GetEffectiveRules(tab))
            : LibraryQuery.ApplySort(result, sort, tab);
    }

    var list = result.ToList();

    // DOMAIN_RULES §7.3 — dao TOAN BO list SAU khi sort xong (mismatch M18).
    if (tab is not ("queue" or "console") && reverseSort == true) list.Reverse();

    var totalItems = list.Count;
    var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)size));

    // Tab queue tra toan bo, khong phan trang — DOMAIN_RULES §8.1.
    var pageItems = tab == "queue"
        ? list
        : list.Skip((pageNum - 1) * size).Take(size).ToList();

    // DLC card thay ngay phat hanh bang chip base game. Ten base game lay tu chinh
    // `games` (toan bo thu vien cua owner da nap) nen khong ton them truy van.
    var baseNames = games
        .GroupBy(g => g.Id)
        .ToDictionary(grp => grp.Key, grp => grp.First().Name);

    var items = pageItems
        .Select(g =>
        {
            var dto = ToDto(g, launchTargets.CanLaunch(g));
            return g.BaseGameId is not null && baseNames.TryGetValue(g.BaseGameId, out var baseName)
                ? dto with { BaseGameName = baseName }
                : dto;
        })
        .ToList();

    return Results.Ok(new GameListResponseDto(
        Items: items,
        Page: pageNum,
        PageSize: size,
        TotalItems: totalItems,
        TotalPages: totalPages,
        ActiveFilterCount: tab is "queue" or "console" ? 0 : filter.ActiveCount,
        CardHints: LibraryQuery.BuildCardHints(sort, tab, scope)));
});

app.MapGet("/api/games/{id}", async (HttpContext context, MasterPlayContext db, string id) =>
{
    if (!TryGetLibraryOwnerKey(context, out var ownerKey))
        return AuthErrorResult(401, "AUTH_REQUIRED", "Sign in or continue as guest to view your library.");
    var game = await LoadGameDetail(db, id, ownerKey);
    if (game is null)
    {
        return Results.NotFound(new
        {
            error = new { code = "GAME_NOT_FOUND", message = $"Game not found: {id}", details = new { gameId = id } }
        });
    }

    var baseGameName = game.BaseGameId is null
        ? null
        : await db.Games.AsNoTracking()
            .Where(g => g.Id == game.BaseGameId)
            .Select(g => g.Name)
            .FirstOrDefaultAsync();

    return Results.Ok(ToGameDetailDto(game, baseGameName, (await LaunchTargets.LoadAsync(db)).CanLaunch(game)));
});

app.MapGet("/api/games/base-candidates", async (
    HttpContext context,
    MasterPlayContext db,
    string? excludeId,
    string? q) =>
{
    if (!TryGetLibraryOwnerKey(context, out var ownerKey))
        return AuthErrorResult(401, "AUTH_REQUIRED", "Sign in or continue as guest to view your library.");
    return Results.Ok(await db.Games.AsNoTracking()
    .Where(game => game.LibraryOwnerKey == ownerKey && !game.IsDisabled && !game.IsDlc && game.Id != excludeId)
    .Where(game => string.IsNullOrWhiteSpace(q) || game.Name.Contains(q))
    .OrderBy(game => game.Name)
    .Select(game => new BaseGameCandidateDto(game.Id, game.Name))
    .ToListAsync());
});

app.MapGet("/api/games/{id}/external-links", async (HttpContext context, MasterPlayContext db, string id) =>
{
    if (!TryGetLibraryOwnerKey(context, out var ownerKey))
        return AuthErrorResult(401, "AUTH_REQUIRED", "Sign in or continue as guest to view game links.");
    var game = await db.Games.AsNoTracking()
        .Where(g => g.Id == id && g.LibraryOwnerKey == ownerKey && !g.IsDisabled)
        .Select(g => new { g.Name, g.SlugName })
        .SingleOrDefaultAsync();

    if (game is null)
    {
        return Results.NotFound(new
        {
            error = new { code = "GAME_NOT_FOUND", message = $"Game not found: {id}" }
        });
    }

    var hltbUrl = "https://howlongtobeat.com/?q=" + Uri.EscapeDataString(game.Name);
    var metacriticUrl = string.IsNullOrWhiteSpace(game.SlugName)
        ? string.Empty
        : "https://www.metacritic.com/game/" + Uri.EscapeDataString(game.SlugName) + "/";

    return Results.Ok(new ExternalLinksDto(hltbUrl, metacriticUrl));
});

app.MapPost("/api/fs/reveal", (RevealPathRequestDto body) =>
{
    if (!OperatingSystem.IsWindows())
    {
        return Results.Json(new
        {
            error = new
            {
                code = "NATIVE_NOT_AVAILABLE",
                message = "Opening folders is available only in the local Windows app."
            }
        }, statusCode: StatusCodes.Status501NotImplemented);
    }

    if (string.IsNullOrWhiteSpace(body.Path))
    {
        return Results.BadRequest(new
        {
            error = new { code = "INVALID_PATH", message = "Folder path is required." }
        });
    }

    string fullPath;
    try
    {
        fullPath = Path.GetFullPath(body.Path.Trim());
    }
    catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
    {
        return Results.BadRequest(new
        {
            error = new { code = "INVALID_PATH", message = "Folder path is invalid." }
        });
    }

    if (!Directory.Exists(fullPath))
    {
        return Results.NotFound(new
        {
            error = new { code = "FOLDER_NOT_FOUND", message = $"Folder not found: {fullPath}" }
        });
    }

    try
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            ArgumentList = { fullPath },
            UseShellExecute = true,
        });
        return Results.NoContent();
    }
    catch (Exception exception)
    {
        return Results.Json(new
        {
            error = new
            {
                code = "FOLDER_OPEN_FAILED",
                message = $"Unable to open folder: {exception.Message}"
            }
        }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapPost("/api/fs/pick-folder", async () =>
{
    if (!OperatingSystem.IsWindows())
    {
        return Results.Json(TaxonomyError(
            "NATIVE_NOT_AVAILABLE",
            "Folder selection is available only in the local Windows app."),
            statusCode: StatusCodes.Status501NotImplemented);
    }

    try
    {
        return Results.Ok(new SelectedPathDto(await NativePicker.PickFolderAsync()));
    }
    catch (Exception exception)
    {
        return Results.Json(TaxonomyError("FOLDER_PICK_FAILED", exception.Message),
            statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapPost("/api/fs/pick-file", async (PickFileRequestDto body) =>
{
    if (!OperatingSystem.IsWindows())
    {
        return Results.Json(TaxonomyError(
            "NATIVE_NOT_AVAILABLE",
            "File selection is available only in the local Windows app."),
            statusCode: StatusCodes.Status501NotImplemented);
    }

    var filter = body.Filter?.Trim();
    if (filter is not ("exe-or-shortcut" or "database" or "json"))
    {
        return Results.BadRequest(TaxonomyError("INVALID_FILE_FILTER", "The file filter is invalid."));
    }

    try
    {
        return Results.Ok(new SelectedPathDto(await NativePicker.PickFileAsync(filter)));
    }
    catch (Exception exception)
    {
        return Results.Json(TaxonomyError("FILE_PICK_FAILED", exception.Message),
            statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapPut("/api/games/{id}", async (
    HttpContext context,
    MasterPlayContext db,
    ImageAutoSyncQueue imageUploads,
    string id,
    GameUpsertDto body) =>
{
    if (!TryGetLibraryOwnerKey(context, out var ownerKey))
        return AuthErrorResult(401, "AUTH_REQUIRED", "Sign in or continue as guest to update games.");
    return await SaveGame(db, imageUploads, id, body, create: false, ownerKey);
});

app.MapPost("/api/games", async (
    HttpContext context,
    MasterPlayContext db,
    ImageAutoSyncQueue imageUploads,
    GameUpsertDto body) =>
{
    if (!TryGetLibraryOwnerKey(context, out var ownerKey))
        return AuthErrorResult(401, "AUTH_REQUIRED", "Sign in or continue as guest to add games.");
    return await SaveGame(db, imageUploads, null, body, create: true, ownerKey);
});

app.MapPost("/api/games/{id}/playtime", async (
    HttpContext context,
    MasterPlayContext db,
    string id,
    AdjustPlaytimeRequestDto body) =>
{
    if (!TryGetLibraryOwnerKey(context, out var ownerKey))
        return AuthErrorResult(401, "AUTH_REQUIRED", "Sign in or continue as guest to update playtime.");
    var game = await db.Games.SingleOrDefaultAsync(g => g.Id == id && g.LibraryOwnerKey == ownerKey && !g.IsDisabled);
    if (game is null)
    {
        return Results.NotFound(new
        {
            error = new { code = "GAME_NOT_FOUND", message = $"Game not found: {id}" }
        });
    }

    var currentSeconds = Math.Max(0, game.TotalPlayTimeSeconds ?? 0);
    var deltaSeconds = (long)body.DeltaMinutes * 60L;
    game.TotalPlayTimeSeconds = (int)Math.Clamp(currentSeconds + deltaSeconds, 0L, int.MaxValue);
    await db.SaveChangesAsync();

    return Results.Ok(await ReloadGameDetailDto(db, id, ownerKey));
});

app.MapPost("/api/games/{id}/disable", async (HttpContext context, MasterPlayContext db, string id) =>
{
    if (!TryGetLibraryOwnerKey(context, out var ownerKey))
        return AuthErrorResult(401, "AUTH_REQUIRED", "Sign in or continue as guest to update your library.");
    var game = await db.Games
        .Include(g => g.GamePosition)
        .SingleOrDefaultAsync(g => g.Id == id && g.LibraryOwnerKey == ownerKey && !g.IsDisabled);
    if (game is null)
    {
        return Results.NotFound(new
        {
            error = new { code = "GAME_NOT_FOUND", message = $"Game not found: {id}" }
        });
    }

    await using var transaction = await db.Database.BeginTransactionAsync();
    game.IsDisabled = true;
    if (game.GamePosition is not null)
    {
        db.GamePositions.Remove(game.GamePosition);
    }
    await db.SaveChangesAsync();
    await transaction.CommitAsync();

    return Results.NoContent();
});

app.MapPost("/api/games/{id}/launch", async (HttpContext context, MasterPlayContext db, string id) =>
{
    if (!TryGetLibraryOwnerKey(context, out var ownerKey))
        return AuthErrorResult(401, "AUTH_REQUIRED", "Sign in or continue as guest to launch games.");
    if (!OperatingSystem.IsWindows())
        return Results.Json(TaxonomyError("NATIVE_NOT_AVAILABLE", "Launching is available only in the local Windows app."),
            statusCode: StatusCodes.Status501NotImplemented);

    var game = await db.Games.AsNoTracking().SingleOrDefaultAsync(g => g.Id == id && g.LibraryOwnerKey == ownerKey && !g.IsDisabled);
    if (game is null) return Results.NotFound(TaxonomyError("GAME_NOT_FOUND", $"Game not found: {id}"));
    if (game.IsDlc) return Results.BadRequest(TaxonomyError("DLC_LAUNCH_NOT_SUPPORTED", "Launch the base game instead."));

    if (string.IsNullOrWhiteSpace(game.LaunchTargetPath) && string.IsNullOrWhiteSpace(game.ExeName))
        return Results.NotFound(TaxonomyError("LAUNCH_TARGET_NOT_FOUND", "No launch target or detected process is available."));
    var launchPath = (await LaunchTargets.LoadAsync(db)).ResolveExistingPath(game);
    if (launchPath is null)
        return Results.NotFound(TaxonomyError("LAUNCH_FILE_NOT_FOUND",
            $"Launch file not found: {game.LaunchTargetPath ?? game.ExeName}"));

    try
    {
        Process.Start(new ProcessStartInfo(launchPath) { UseShellExecute = true });
        return Results.NoContent();
    }
    catch (Exception exception)
    {
        return Results.Json(TaxonomyError("GAME_LAUNCH_FAILED", exception.Message),
            statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapPost("/api/games/{id}/backup-folder/generate", async (HttpContext context, MasterPlayContext db, string id) =>
{
    if (!TryGetLibraryOwnerKey(context, out var ownerKey))
        return AuthErrorResult(401, "AUTH_REQUIRED", "Sign in or continue as guest to manage game folders.");
    if (!MasterPlay.Core.FeatureFlags.GameBackup)
    {
        return Results.Json(TaxonomyError(
            "FEATURE_DISABLED",
            "Game backup is temporarily disabled."),
            statusCode: StatusCodes.Status501NotImplemented);
    }
    if (!OperatingSystem.IsWindows())
    {
        return Results.Json(TaxonomyError(
            "NATIVE_NOT_AVAILABLE",
            "Backup folders are available only in the local Windows app."),
            statusCode: StatusCodes.Status501NotImplemented);
    }

    var game = await db.Games.SingleOrDefaultAsync(g => g.Id == id && g.LibraryOwnerKey == ownerKey && !g.IsDisabled);
    if (game is null)
        return Results.NotFound(TaxonomyError("GAME_NOT_FOUND", $"Game not found: {id}"));
    if (game.IsDlc)
        return Results.BadRequest(TaxonomyError("DLC_BACKUP_NOT_SUPPORTED", "DLC games do not have a separate backup folder."));

    if (!string.IsNullOrWhiteSpace(game.BackupFolderPath) && Directory.Exists(game.BackupFolderPath))
        return Results.Ok(new BackupFolderDto(game.BackupFolderPath));

    var backupRoot = JsonService.GetBackupRootPath();
    if (string.IsNullOrWhiteSpace(backupRoot))
        return Results.BadRequest(TaxonomyError("BACKUP_ROOT_MISSING", "Configure a backup root before generating a folder."));

    try
    {
        Directory.CreateDirectory(backupRoot);
        var folderName = MasterPlay.Infrastructure.Data.Services.DataService.ValidFolderName(game.Name);
        if (string.IsNullOrWhiteSpace(folderName)) folderName = "Game";

        var backupPath = Path.Combine(backupRoot, folderName);
        for (var suffix = 1; Directory.Exists(backupPath); suffix++)
            backupPath = Path.Combine(backupRoot, $"{folderName} ({suffix})");

        Directory.CreateDirectory(backupPath);
        if (!string.IsNullOrWhiteSpace(game.SaveFolderPath))
            await File.WriteAllTextAsync(Path.Combine(backupPath, "SaveLocation.txt"), game.SaveFolderPath);

        game.BackupFolderPath = backupPath;
        await db.SaveChangesAsync();
        return Results.Ok(new BackupFolderDto(backupPath));
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
    {
        return Results.Json(TaxonomyError("BACKUP_FOLDER_CREATE_FAILED", exception.Message),
            statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapPost("/api/game-metadata/preview", async (
    HttpContext context,
    MasterPlayContext db,
    GameMetadataPreviewService previewService,
    GameMetadataPreviewRequestDto body,
    CancellationToken cancellationToken) =>
{
    if (!TryGetLibraryOwnerKey(context, out _))
        return AuthErrorResult(401, "AUTH_REQUIRED", "Sign in or continue as guest to preview game metadata.");

    var name = body.Name?.Trim();
    var source = body.Source?.Trim();
    if (string.IsNullOrWhiteSpace(name))
        return Results.BadRequest(TaxonomyError("NAME_REQUIRED", "A game name is required to preview metadata."));
    if (source is not ("metadata" or "card-cover" or "cover" or "portrait-cover"))
        return Results.BadRequest(TaxonomyError("INVALID_SYNC_SOURCE", $"Unsupported sync source: {source}"));

    // Preview only: kho metadata truoc, thieu moi goi provider; KHONG ghi DB.
    // Thu tu nguon va cach xu ly loi: xem GameMetadataPreviewService.
    var result = await previewService.PreviewAsync(
        name,
        source,
        async token => await db.Series.AsNoTracking().ToListAsync(token),
        cancellationToken);
    return result.Preview is not null
        ? Results.Ok(result.Preview)
        : Results.Json(TaxonomyError(result.ErrorCode!, result.ErrorMessage!), statusCode: result.StatusCode);
});

app.MapPost("/api/games/{gameId}/finishes", async (
    HttpContext context,
    MasterPlayContext db,
    string gameId,
    FinishGameRequestDto body) =>
{
    if (!TryGetLibraryOwnerKey(context, out var ownerKey))
        return AuthErrorResult(401, "AUTH_REQUIRED", "Sign in or continue as guest to update finish history.");
    if (body.Score is not null && !IsValidRating(body.Score))
    {
        return Results.BadRequest(new
        {
            error = new
            {
                code = "INVALID_FINISH_SCORE",
                message = "Finish score must be between 0 and 11 in half-point steps."
            }
        });
    }

    if (!TryReadFinishedAt(body.FinishedYear, body.FinishedMonth, out var finishedAt, out var hasMonth))
        return InvalidFinishTimeResult();

    var game = await db.Games.SingleOrDefaultAsync(g => g.Id == gameId && g.LibraryOwnerKey == ownerKey && !g.IsDisabled);
    if (game is null)
    {
        return Results.NotFound(new
        {
            error = new { code = "GAME_NOT_FOUND", message = $"Game not found: {gameId}" }
        });
    }

    var progressStatuses = await db.ProgressStatuses.ToListAsync();
    var done = progressStatuses.FirstOrDefault(p => LibraryQuery.ProgressSystemKey(p) == "Done");
    if (done is null)
    {
        return Results.Json(new
        {
            error = new
            {
                code = "DONE_PROGRESS_MISSING",
                message = "The Done progress catalog entry is unavailable."
            }
        }, statusCode: StatusCodes.Status409Conflict);
    }

    // Diem chi BAT BUOC o lan hoan thanh dau tien; cac lan sau giu nguyen
    // game.Score. Khong con luu diem theo tung lan (DOMAIN_RULES §9.2).
    var firstFinish = !await db.GameFinishes.AnyAsync(f => f.GameId == game.Id);
    if (firstFinish && body.Score is null)
    {
        return Results.BadRequest(new
        {
            error = new
            {
                code = "FINISH_SCORE_REQUIRED",
                message = "A score is required the first time a game is finished."
            }
        });
    }

    await using var transaction = await db.Database.BeginTransactionAsync();
    if (body.Score is not null) game.Score = body.Score;
    game.ProgressId = done.Id;
    db.GameFinishes.Add(new GameFinish
    {
        GameId = game.Id,
        FinishedAt = finishedAt,
        HasMonth = hasMonth,
    });
    await db.SaveChangesAsync();
    await transaction.CommitAsync();

    return Results.Ok(await ReloadGameDetailDto(db, game.Id, ownerKey));
});

app.MapPut("/api/games/{gameId}/finishes/{finishId:int}", async (
    HttpContext context,
    MasterPlayContext db,
    string gameId,
    int finishId,
    UpdateFinishTimeRequestDto body) =>
{
    if (!TryGetLibraryOwnerKey(context, out var ownerKey))
        return AuthErrorResult(401, "AUTH_REQUIRED", "Sign in or continue as guest to update finish history.");
    if (body.FinishedYear is null ||
        !TryReadFinishedAt(body.FinishedYear, body.FinishedMonth, out var finishedAt, out var hasMonth))
    {
        return InvalidFinishTimeResult();
    }

    if (!await db.Games.AnyAsync(g => g.Id == gameId && g.LibraryOwnerKey == ownerKey && !g.IsDisabled))
    {
        return Results.NotFound(new
        {
            error = new { code = "GAME_NOT_FOUND", message = $"Game not found: {gameId}" }
        });
    }

    var finish = await db.GameFinishes.SingleOrDefaultAsync(f => f.Id == finishId && f.GameId == gameId);
    if (finish is null)
    {
        return Results.NotFound(new
        {
            error = new
            {
                code = "GAME_FINISH_NOT_FOUND",
                message = $"Finish history entry not found: {finishId}"
            }
        });
    }

    finish.FinishedAt = finishedAt;
    finish.HasMonth = hasMonth;
    await db.SaveChangesAsync();

    return Results.Ok(await ReloadGameDetailDto(db, gameId, ownerKey));
});

// Xoa mot moc hoan thanh. Xem DOMAIN_RULES 9.6.
app.MapDelete("/api/games/{gameId}/finishes/{finishId:int}", async (
    HttpContext context,
    MasterPlayContext db,
    string gameId,
    int finishId) =>
{
    if (!TryGetLibraryOwnerKey(context, out var ownerKey))
        return AuthErrorResult(401, "AUTH_REQUIRED", "Sign in or continue as guest to update finish history.");

    var game = await db.Games.SingleOrDefaultAsync(g => g.Id == gameId && g.LibraryOwnerKey == ownerKey && !g.IsDisabled);
    if (game is null)
    {
        return Results.NotFound(new
        {
            error = new { code = "GAME_NOT_FOUND", message = $"Game not found: {gameId}" }
        });
    }

    var finish = await db.GameFinishes.SingleOrDefaultAsync(f => f.Id == finishId && f.GameId == gameId);
    if (finish is null)
    {
        return Results.NotFound(new
        {
            error = new
            {
                code = "GAME_FINISH_NOT_FOUND",
                message = $"Finish history entry not found: {finishId}"
            }
        });
    }

    await using var transaction = await db.Database.BeginTransactionAsync();
    db.GameFinishes.Remove(finish);
    await db.SaveChangesAsync();

    // Khong con moc nao thi game khong con la "Done" nua. De trong Progress chu
    // KHONG doan sang mot trang thai khac — khong ai biet nguoi choi dang o dau
    // trong game. Score giu nguyen: no la danh gia, khong phai bang chung hoan
    // thanh.
    if (!await db.GameFinishes.AnyAsync(f => f.GameId == gameId))
    {
        game.ProgressId = null;
        await db.SaveChangesAsync();
    }

    await transaction.CommitAsync();

    return Results.Ok(await ReloadGameDetailDto(db, gameId, ownerKey));
});

// Queue co endpoint rieng vi GET phai chuan hoa + persist PlayOrder, con PUT phai
// ap quy tac Progress trong mot transaction. Frontend chi gui danh sach id.
app.MapGet("/api/queue", async (HttpContext context, MasterPlayContext db) =>
{
    if (!TryGetLibraryOwnerKey(context, out var ownerKey))
        return AuthErrorResult(401, "AUTH_REQUIRED", "Sign in or continue as guest to view your queue.");
    var games = await LoadQueueCandidates(db, ownerKey);
    var queue = LibraryQuery.ApplySort(LibraryQuery.ApplyTab(games, "queue"), "default", "queue")
        .ToList();

    await PersistQueuePositions(db, queue);
    var launchTargets = await LaunchTargets.LoadAsync(db);
    return Results.Ok(queue.Select(g => ToDto(g, launchTargets.CanLaunch(g))).ToList());
});

app.MapPut("/api/queue/order", async (HttpContext context, MasterPlayContext db, QueueOrderRequestDto body) =>
{
    if (!TryGetLibraryOwnerKey(context, out var ownerKey))
        return AuthErrorResult(401, "AUTH_REQUIRED", "Sign in or continue as guest to update your queue.");
    var games = await LoadQueueCandidates(db, ownerKey);
    var currentQueue = LibraryQuery.ApplyTab(games, "queue").ToList();

    var requestedIds = body.GameIds?.ToList() ?? [];
    if (requestedIds.Count != requestedIds.Distinct(StringComparer.Ordinal).Count())
    {
        return Results.BadRequest(new
        {
            error = new { code = "QUEUE_DUPLICATE_ID", message = "Queue order contains duplicate game ids." }
        });
    }

    var currentIds = currentQueue.Select(g => g.Id).ToHashSet(StringComparer.Ordinal);
    if (requestedIds.Count != currentIds.Count || requestedIds.Any(id => !currentIds.Contains(id)))
    {
        return Results.BadRequest(new
        {
            error = new
            {
                code = "QUEUE_ORDER_MISMATCH",
                message = "Queue order must contain every current queue game exactly once."
            }
        });
    }

    var byId = currentQueue.ToDictionary(g => g.Id, StringComparer.Ordinal);
    var reordered = requestedIds.Select(id => byId[id]).ToList();

    // Game dang choi phai chiem tron dau danh sach. Client da chan keo tha vuot
    // ranh gioi nay; chan them o day vi PUT la noi DUY NHAT ghi thu tu xuong DB.
    var lastPlaying = reordered.FindLastIndex(g => LibraryQuery.ProgressSystemKey(g.Progress) == "Playing");
    var firstNonPlaying = reordered.FindIndex(g => LibraryQuery.ProgressSystemKey(g.Progress) != "Playing");
    if (firstNonPlaying >= 0 && lastPlaying > firstNonPlaying)
    {
        return Results.BadRequest(new
        {
            error = new
            {
                code = "QUEUE_PLAYING_ORDER",
                message = "Playing games must occupy the top of the queue."
            }
        });
    }

    var progressStatuses = await db.ProgressStatuses.ToListAsync();
    var nextPlay = progressStatuses.FirstOrDefault(p => LibraryQuery.ProgressSystemKey(p) == "NextPlay");
    var intended = progressStatuses.FirstOrDefault(p => LibraryQuery.ProgressSystemKey(p) == "Intended");

    if (nextPlay is null || intended is null)
    {
        return Results.Json(new
        {
            error = new
            {
                code = "QUEUE_PROGRESS_MISSING",
                message = "Queue progress catalog is incomplete."
            }
        }, statusCode: StatusCodes.Status500InternalServerError);
    }

    var nonPlayingIndex = 0;
    foreach (var game in reordered)
    {
        if (LibraryQuery.ProgressSystemKey(game.Progress) == "Playing") continue;

        var target = nonPlayingIndex < 3 ? nextPlay : intended;
        game.Progress = target;
        game.ProgressId = target.Id;
        nonPlayingIndex++;
    }

    await using var transaction = await db.Database.BeginTransactionAsync();
    await PersistQueuePositions(db, reordered, saveChanges: false);
    await db.SaveChangesAsync();
    await transaction.CommitAsync();

    var launchTargets = await LaunchTargets.LoadAsync(db);
    return Results.Ok(reordered.Select(g => ToDto(g, launchTargets.CanLaunch(g))).ToList());
});

// Catalog: Progress / Ownership — nguon `displayName`, `colorCode`, `iconData`, `sortOrder`.
// Client KHONG duoc hardcode 6 literal nhu ban Lovable (DOMAIN_RULES §3, §14).
app.MapGet("/api/catalog/{kind}", async (MasterPlayContext db, string kind) => kind switch
{
    "progress" => Results.Ok((await db.ProgressStatuses.AsNoTracking().OrderBy(p => p.SortOrder).ToListAsync())
        .Select(ToCatalog).ToList()),
    "ownership" => Results.Ok((await db.OwnershipStatuses.AsNoTracking().OrderBy(o => o.SortOrder).ToListAsync())
        .Select(ToCatalogOwn).ToList()),
    _ => Results.BadRequest(new { error = new { code = "BAD_KIND", message = $"kind khong hop le: {kind}" } }),
});

// Taxonomy (genre/theme/series + nhom theme): MasterPlay.Host/Taxonomy/TaxonomyEndpoints.cs
app.MapTaxonomyEndpoints();

// Thong bao game sap ra mat: MasterPlay.Host/Notifications/ReleaseNotificationEndpoints.cs
app.MapReleaseNotificationEndpoints();

// Dashboard — BACKEND tinh, khong tinh o client (T1).
app.MapGet("/api/dashboard/stats", async (HttpContext context, MasterPlayContext db) =>
{
    if (!TryGetLibraryOwnerKey(context, out var ownerKey))
        return AuthErrorResult(401, "AUTH_REQUIRED", "Sign in or continue as guest to view dashboard stats.");
    // Lay CA game bi vo hieu hoa (phan "suc khoe du lieu" can dem chung) roi moi
    // loc — khac truoc, truoc loc ngay o truy van.
    var all = await db.Games.AsNoTracking()
        .Where(game => game.LibraryOwnerKey == ownerKey)
        .Include(g => g.Progress)
        .Include(g => g.Ownership)
        .Include(g => g.Series)
        .Include(g => g.GameGenres)
        .Include(g => g.Finishes)
        .ToListAsync();

    var games = all.Where(g => !g.IsDisabled).ToList();

    string Key(Game g) => LibraryQuery.ProgressSystemKey(g.Progress);

    // Diem trung binh: bo qua Peak Score 11 va gia tri 0 (chua cham diem).
    var scored = games.Where(g => g.Score is > 0 and < 11).Select(g => g.Score!.Value).ToList();

    var top = games
        .OrderByDescending(g => g.TotalPlayTimeSeconds ?? 0)
        .Take(5)
        .Select(g => new
        {
            id = g.Id,
            name = g.Name,
            bannerPath = g.BannerPath,
            totalPlaytimeSeconds = g.TotalPlayTimeSeconds ?? 0,
        })
        .ToList();

    // ---------- Nhom 1: thong ke thu vien ----------

    // Mot game hoan thanh nhieu lan thi dem tung lan: day la LICH SU, khong phai
    // so game.
    var finishesByYear = games
        .SelectMany(g => g.Finishes)
        .GroupBy(f => f.FinishedAt.Year)
        .OrderBy(grp => grp.Key)
        .Select(grp => new { year = grp.Key, count = grp.Count() })
        .ToList();

    // Mau lay tu catalog chu KHONG hardcode — nguoi dung doi mau trong Settings
    // thi bieu do doi theo (DOMAIN_RULES §3, §14).
    var progressBreakdown = games
        // Group by the stable catalog FK. With AsNoTracking, grouping by the
        // navigation object creates one bucket per game instance even when
        // several games share the same ProgressStatus row.
        .GroupBy(g => g.ProgressId)
        .Select(grp =>
        {
            var catalog = grp.Select(g => g.Progress).FirstOrDefault();
            return new
            {
                name = catalog?.Name ?? "",
                systemKey = LibraryQuery.ProgressSystemKey(catalog),
                colorCode = catalog?.ColorCode,
                count = grp.Count(),
            };
        })
        .OrderByDescending(x => x.count).ThenBy(x => x.name)
        .ToList();

    var ownershipBreakdown = games
        .GroupBy(g => g.OwnershipId)
        .Select(grp =>
        {
            var catalog = grp.Select(g => g.Ownership).FirstOrDefault();
            return new
            {
                name = catalog?.Name ?? "",
                systemKey = LibraryQuery.OwnershipSystemKey(catalog),
                colorCode = catalog?.ColorCode,
                count = grp.Count(),
            };
        })
        .OrderByDescending(x => x.count).ThenBy(x => x.name)
        .ToList();

    var genreNames = await db.Genres.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Name);
    var topGenres = games
        .SelectMany(g => g.GameGenres)
        .GroupBy(gg => gg.GenreId)
        .Select(grp => new { name = genreNames.TryGetValue(grp.Key, out var n) ? n : "", count = grp.Count() })
        .Where(x => x.name.Length > 0)
        .OrderByDescending(x => x.count).ThenBy(x => x.name)
        .Take(8).ToList();

    var topSeries = games
        .Where(g => g.Series != null)
        .GroupBy(g => g.Series!.Name)
        .Select(grp => new { name = grp.Key, count = grp.Count() })
        .OrderByDescending(x => x.count).ThenBy(x => x.name)
        .Take(8).ToList();

    var topPublishers = games
        .Where(g => !string.IsNullOrWhiteSpace(g.Publisher))
        .GroupBy(g => g.Publisher!.Trim())
        .Select(grp => new { name = grp.Key, count = grp.Count() })
        .OrderByDescending(x => x.count).ThenBy(x => x.name)
        .Take(6).ToList();

    var topDevelopers = games
        .Where(g => !string.IsNullOrWhiteSpace(g.Developer))
        .GroupBy(g => g.Developer!.Trim())
        .Select(grp => new { name = grp.Key, count = grp.Count() })
        .OrderByDescending(x => x.count).ThenBy(x => x.name)
        .Take(6).ToList();

    // Diem toi cham vs Metacritic. Chi lay game co CA HAI. Bo Peak Score 11 vi
    // day la danh dau dac biet, khong phai diem tren thang 10 de so sanh.
    var paired = games
        .Where(g => g.Score is > 0 and < 11 && g.MetaScore is > 0)
        .Select(g => new { g.Name, Mine = g.Score!.Value, Critic = g.MetaScore!.Value / 10.0 })
        .ToList();

    var scoreVsCritics = new
    {
        pairedGames = paired.Count,
        averageMine = paired.Count > 0 ? Math.Round(paired.Average(x => x.Mine), 2) : (double?)null,
        averageCritic = paired.Count > 0 ? Math.Round(paired.Average(x => x.Critic), 2) : (double?)null,
        higher = paired.Count(x => x.Mine > x.Critic),
        lower = paired.Count(x => x.Mine < x.Critic),
        equal = paired.Count(x => Math.Abs(x.Mine - x.Critic) < 0.001),
        points = paired
            .OrderByDescending(x => x.Mine - x.Critic)
            .Select(x => new { name = x.Name, mine = x.Mine, critic = Math.Round(x.Critic, 1) })
            .ToList(),
    };

    // Peak Score 11 gop vao coc 10 de giu tuong thich voi bieu do thang 10.
    var scoreDistribution = games
        .Where(g => g.Score is > 0)
        .GroupBy(g => g.Score!.Value == 11 ? 10.0 : g.Score!.Value)
        .OrderBy(grp => grp.Key)
        .Select(grp => new { score = grp.Key, count = grp.Count() })
        .ToList();

    var gamesByReleaseYear = games
        .Where(g => g.PublishDate.HasValue)
        .GroupBy(g => g.PublishDate!.Value.Year)
        .OrderBy(grp => grp.Key)
        .Select(grp => new { year = grp.Key, count = grp.Count() })
        .ToList();

    var today = DateOnly.FromDateTime(DateTime.Now);
    var upcoming = games
        .Where(g => g.PublishDate.HasValue && g.PublishDate.Value > today)
        .OrderBy(g => g.PublishDate!.Value)
        .Take(8)
        .Select(g => new
        {
            id = g.Id,
            name = g.Name,
            bannerPath = g.BannerPath,
            publishDate = g.PublishDate!.Value.ToString("yyyy-MM-dd"),
            daysLeft = g.PublishDate!.Value.DayNumber - today.DayNumber,
        })
        .ToList();

    // ---------- Nhom 4: suc khoe du lieu ----------
    // Moi muc la mot VIEC CAN LAM kem so game dang dinh.
    var dataHealth = new
    {
        activeGames = games.Count,
        missingSeries = games.Count(g => g.SeriesId is null),
        missingGenres = games.Count(g => g.GameGenres.Count == 0),
        missingSlug = games.Count(g => string.IsNullOrWhiteSpace(g.SlugName)),
        missingBanner = games.Count(g => string.IsNullOrWhiteSpace(g.BannerPath)),
        missingPoster = games.Count(g => string.IsNullOrWhiteSpace(g.PosterPath)),
        missingPublishDate = games.Count(g => g.PublishDate is null),
        missingProgress = games.Count(g => g.ProgressId is null),
        missingOwnership = games.Count(g => g.OwnershipId is null),
        // Danh dau xong ma khong co moc hoan thanh nao -> lich su bi thung.
        doneWithoutFinish = games.Count(g => Key(g) == "Done" && g.Finishes.Count == 0),
        disabledGames = all.Count(g => g.IsDisabled),
    };

    return Results.Ok(new
    {
        totalGames = games.Count,
        finishedGames = games.Count(g => Key(g) == "Done"),
        playingGames = games.Count(g => Key(g) == "Playing"),
        queuedGames = games.Count(g => Key(g) is "Playing" or "Nextplay" or "NextPlay" or "Planned" or "Intended"),
        totalPlaytimeSeconds = games.Sum(g => (long)(g.TotalPlayTimeSeconds ?? 0)),
        averageScore = scored.Count > 0 ? Math.Round(scored.Average(), 1) : (double?)null,
        topPlayed = top,
        finishesByYear,
        progressBreakdown,
        ownershipBreakdown,
        topGenres,
        topSeries,
        topPublishers,
        topDevelopers,
        scoreVsCritics,
        scoreDistribution,
        gamesByReleaseYear,
        upcoming,
        dataHealth,
    });
});

app.Run();

static string? BearerToken(HttpRequest request)
{
    var header = request.Headers.Authorization.ToString();
    return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
        ? header["Bearer ".Length..].Trim()
        : null;
}

/// <summary>
/// Guard cho moi endpoint /api/admin/**.
///
/// Role doc tu <c>QuestLogAccount</c> — do middleware dat sau khi gateway xac thuc
/// access token voi Supabase, nen day la role tu PROFILE chu khong phai tu payload
/// client gui len (spec §2).
///
/// Workspace mode cua UI KHONG duoc tham gia vao quyet dinh nay: mode la rao chan
/// UX, khong phai rao chan bao mat.
/// </summary>
static IResult? RequireAdmin(HttpContext context, out AuthAccountDto account)
{
    account = null!;

    if (context.Items["QuestLogGuest"] is true)
        return AuthErrorResult(403, "ADMIN_REQUIRED", "This action requires an admin account.");

    if (context.Items["QuestLogAccount"] is not AuthAccountDto signedIn || string.IsNullOrWhiteSpace(signedIn.Id))
        return AuthErrorResult(401, "AUTH_REQUIRED", "Sign in to continue.");

    if (!string.Equals(signedIn.Role, "admin", StringComparison.OrdinalIgnoreCase))
        return AuthErrorResult(403, "ADMIN_REQUIRED", "This action requires an admin account.");

    account = signedIn;
    return null;
}

/// <summary>
/// Chan o bien truoc khi cham toi Supabase. Kiem tra trung ten va cac rang buoc
/// nghiep vu khac nam trong ham SQL, noi chung chay cung transaction voi audit.
/// </summary>
static IResult? ValidateSeriesName(string? name)
{
    var trimmed = name?.Trim();
    if (string.IsNullOrEmpty(trimmed) || trimmed.Length > 200)
    {
        return Results.UnprocessableEntity(new
        {
            error = new { code = "SERIES_NAME_INVALID", message = "Series name must contain 1-200 characters." }
        });
    }
    return null;
}

/// <summary>
/// Tham so truy van sai — tu choi TRUOC khi cham toi DB. Tra cung hinh dang loi
/// voi moi endpoint admin khac.
/// </summary>
static bool TryParseMetadataCursor(string? cursor, out JsonElement? parsed)
{
    parsed = null;
    if (string.IsNullOrWhiteSpace(cursor)) return true;
    try
    {
        parsed = JsonDocument.Parse(cursor).RootElement.Clone();
        return true;
    }
    catch (JsonException)
    {
        return false;
    }
}

static IResult MetadataParamError(string code, string message) =>
    Results.Json(new { error = new { code, message } }, statusCode: 422);

static IResult IntegrationParamError(string code, string message) =>
    Results.Json(new { error = new { code, message } }, statusCode: 422);

static IResult IntegrationErrorResult(IntegrationStoreException exception) =>
    Results.Json(
        new { error = new { code = exception.Code, message = exception.Message } },
        statusCode: exception.StatusCode);

static IResult MetadataErrorResult(MetadataCatalogException exception)
{
    // Ban ghi kem theo loi phai co CUNG shape voi response thanh cong. UI dung no de
    // doi chieu voi ban dang sua, nen hai shape khac nhau la mot cai bay.
    var details = exception.Details is MetadataAdminGame game
        ? ToAdminMetadataGameDto(game)
        : exception.Details;

    return Results.Json(
        new { error = new { code = exception.Code, message = exception.Message, details } },
        statusCode: exception.StatusCode);
}

static AdminMetadataGameDto ToAdminMetadataGameDto(MetadataAdminGame game) => new(
    game.Id,
    game.IgdbId,
    game.Name,
    game.Developer,
    game.Publisher,
    game.Summary,
    game.PublishDate?.ToString("yyyy-MM-dd"),
    game.IsDlc,
    game.BaseMetadataGameId,
    game.PosterUrl,
    game.BackdropUrl,
    game.BannerUrl,
    game.MetaScore,
    game.AvgPlaytimeHours,
    game.SeriesId,
    game.Revision,
    game.UpdatedAt?.ToString("O"),
    game.ManuallyUnpublished,
    game.IsPublished,
    game.GateFailures);

/// <summary>
/// Dung payload jsonb gui cho ham SQL. Khoa dung snake_case khop signature ham.
///
/// Chan o bien nhung thu re tien va chac chan (dinh dang ngay, do dai chuoi); con
/// rang buoc nghiep vu (trung ten, FK, revision) de ham SQL lam, noi chung chay
/// cung transaction voi audit.
/// </summary>
static IResult? BuildMetadataGamePayload(
    AdminMetadataGameWriteDto? body,
    out Dictionary<string, object?> payload)
{
    payload = [];

    var name = body?.Name?.Trim();
    if (string.IsNullOrEmpty(name) || name.Length > 300)
    {
        return Results.UnprocessableEntity(new
        {
            error = new { code = "GAME_NAME_INVALID", message = "Game name must contain 1-300 characters." }
        });
    }

    string? publishDate = null;
    if (!string.IsNullOrWhiteSpace(body!.PublishDate))
    {
        if (!DateOnly.TryParseExact(
                body.PublishDate.Trim(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            return Results.UnprocessableEntity(new
            {
                error = new { code = "GAME_PUBLISH_DATE_INVALID", message = "Release date must be formatted as YYYY-MM-DD." }
            });
        }
        publishDate = parsed.ToString("yyyy-MM-dd");
    }

    static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    payload = new Dictionary<string, object?>
    {
        ["igdb_id"] = body.IgdbId,
        ["name"] = name,
        ["developer"] = Clean(body.Developer),
        ["publisher"] = Clean(body.Publisher),
        ["summary"] = Clean(body.Summary),
        ["publish_date"] = publishDate,
        ["is_dlc"] = body.IsDlc ?? false,
        ["base_metadata_game_id"] = body.BaseMetadataGameId,
        ["poster_url"] = Clean(body.PosterUrl),
        ["backdrop_url"] = Clean(body.BackdropUrl),
        ["banner_url"] = Clean(body.BannerUrl),
        ["meta_score"] = body.MetaScore,
        ["avg_playtime_hours"] = body.AvgPlaytimeHours,
        ["series_id"] = body.SeriesId,
    };
    return null;
}

static bool TryGetLibraryOwnerKey(HttpContext context, out string ownerKey)
{
    if (context.Items["QuestLogGuest"] is true)
    {
        ownerKey = "guest:local";
        return true;
    }

    if (context.Items["QuestLogAccount"] is AuthAccountDto account && !string.IsNullOrWhiteSpace(account.Id))
    {
        ownerKey = $"user:{account.Id}";
        return true;
    }

    ownerKey = string.Empty;
    return false;
}

/// <summary>
/// Local maintenance command for the library owner on this device. It is deliberately
/// not an HTTP endpoint: a user runs it explicitly, it only fills missing IGDB posters,
/// and it never replaces an image selected in the editor.
/// </summary>
/// <summary>
/// Dien poster cho game chua co. Cung thu tu voi nut Sync cua editor: kho metadata
/// truoc (ke ca ban chua xuat ban vi truot tieu chi, tru ban admin chu dong go),
/// kho khong co poster moi goi IGDB.
/// </summary>
static async Task<int> SyncMissingPostersAsync(IConfiguration configuration)
{
    // Lenh nay chay truoc khi dung DI cua web app: dung rieng mot container nho cho catalog.
    var services = new ServiceCollection()
        .AddLogging(logging => logging.AddConsole())
        .AddSingleton(SupabaseAuthOptions.FromConfiguration(configuration))
        .AddSingleton<SupabaseMetadataCatalog>();
    services.AddHttpClient(nameof(SupabaseMetadataCatalog), client => client.Timeout = TimeSpan.FromSeconds(15));
    await using var provider = services.BuildServiceProvider();
    var catalog = provider.GetRequiredService<SupabaseMetadataCatalog>();

    if (!catalog.IsConfigured && !IgdbService.IsConfigured)
    {
        Console.Error.WriteLine("Neither the metadata catalog (Supabase) nor IGDB is configured.");
        return 2;
    }
    if (!catalog.IsConfigured)
        Console.WriteLine("Metadata catalog is not configured; using IGDB only.");
    if (!IgdbService.IsConfigured)
        Console.WriteLine("IGDB is not configured; using the metadata catalog only.");

    await using var db = new MasterPlayContext();
    var games = await db.Games
        .Where(game => !game.IsDisabled && string.IsNullOrWhiteSpace(game.PosterPath))
        .OrderBy(game => game.Name)
        .ToListAsync();

    var updated = 0;
    var notFound = 0;
    var failed = 0;

    Console.WriteLine($"Poster sync: {games.Count} game(s) missing a poster.");

    foreach (var game in games)
    {
        try
        {
            string? poster = null;
            var from = "metadata";
            if (catalog.IsConfigured)
            {
                var match = await catalog.FindClosestAsync(game.Name, CancellationToken.None);
                poster = string.IsNullOrWhiteSpace(match?.PosterUrl) ? null : match.PosterUrl.Trim();
            }
            if (poster is null && IgdbService.IsConfigured)
            {
                from = "igdb";
                var metadata = game.IgdbId is > 0
                    ? await IgdbService.GetMetadataByIdAsync(game.IgdbId.Value)
                    : await IgdbService.GetMetadataAsync(game.Name);
                poster = string.IsNullOrWhiteSpace(metadata?.CoverUrl) ? null : metadata.CoverUrl;
            }

            if (poster is null)
            {
                notFound++;
                Console.WriteLine($"[not found] {game.Name}");
                continue;
            }

            game.PosterPath = poster;
            await db.SaveChangesAsync();
            updated++;
            Console.WriteLine($"[updated from {from}] {game.Name}");

            // Lenh nay thoat ngay khi xong, khong co hang doi nen: dua anh ve noi luu luon o day.
            var imageSync = await GameImageSync.SyncGameAsync(game.Id);
            foreach (var failure in imageSync.Failures)
                Console.Error.WriteLine($"[image sync failed] {game.Name} ({failure.Role}): {failure.Reason}");
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException or MetadataCatalogException)
        {
            failed++;
            Console.Error.WriteLine($"[failed] {game.Name}: {exception.Message}");
        }
    }

    Console.WriteLine($"Complete. Updated: {updated}; not found: {notFound}; failed: {failed}.");
    return failed == 0 ? 0 : 1;
}

/// <summary>
/// Local maintenance command, same job as the Tray menu item: move every game
/// image on this device to its storage — Cloudinary for admin libraries, this PC
/// for everyone else (DOMAIN_RULES §12). Uses config.json Cloudinary credentials
/// because the Vault warmup only runs when the Host serves HTTP.
/// </summary>
static async Task<int> SyncImagesAsync()
{
    var progress = new Progress<string>(Console.WriteLine);
    var result = await GameImageSync.SyncAllAsync(progress);

    foreach (var failure in result.Failures)
        Console.Error.WriteLine($"[failed] {failure.GameName} ({failure.Role}): {failure.Reason}");
    Console.WriteLine(
        $"Complete. Banner {result.CardCoverUploaded}/{result.CardCoverAlreadySynced}/{result.CardCoverMissing}, " +
        $"backdrop {result.CoverUploaded}/{result.CoverAlreadySynced}/{result.CoverMissing}, " +
        $"poster {result.PortraitCoverUploaded}/{result.PortraitCoverAlreadySynced}/{result.PortraitCoverMissing} " +
        $"(stored/already stored/missing); failed: {result.Failed} of {result.TotalGames} games.");
    return result.Failed == 0 ? 0 : 1;
}

static async Task ClaimLegacyGamesAsync(
    MasterPlayContext db,
    string accountId,
    CancellationToken cancellationToken)
{
    if (string.IsNullOrWhiteSpace(accountId)) return;
    await db.Games
        .Where(game => game.LibraryOwnerKey == "legacy:local")
        .ExecuteUpdateAsync(update => update.SetProperty(game => game.LibraryOwnerKey, $"user:{accountId}"), cancellationToken);
}

static MetadataGameCandidateDto ToMetadataGameCandidateDto(
    MetadataCatalogGame game,
    string libraryStatus) => new(
        Id: game.Id,
        IgdbId: game.IgdbId,
        Name: game.Name,
        Developer: game.Developer,
        Publisher: game.Publisher,
        Summary: game.Summary,
        PublishDate: game.PublishDate?.ToString("yyyy-MM-dd"),
        IsDlc: game.IsDlc,
        IsUpcoming: game.PublishDate.HasValue && game.PublishDate.Value > DateOnly.FromDateTime(DateTime.Now),
        BaseMetadataGameId: game.BaseMetadataGameId,
        PosterUrl: game.PosterUrl,
        BackdropUrl: game.BackdropUrl,
        BannerUrl: game.BannerUrl,
        MetaScore: game.MetaScore,
        AvgPlaytimeHours: game.AvgPlaytimeHours,
        SeriesId: game.SeriesId,
        Revision: game.Revision,
        LibraryStatus: libraryStatus);

static IResult AuthErrorResult(int statusCode, string code, string message) =>
    Results.Json(new { error = new { code, message } }, statusCode: statusCode);

static TrayStatusDto ToTrayStatusDto(TrayControlStatus status) => new(
    true,
    status.Services.Select(service => new TrayServiceStatusDto(service.Name, service.Enabled)).ToList(),
    status.StartWithWindows);

/// <summary>Null = tray khong chay. Loi tu tray (dang bat/tat, scanner dang tat...) giu nguyen ma.</summary>
static IResult TrayCommandResult(TrayControlResponse? reply, int successStatusCode)
{
    if (reply == null)
    {
        return Results.Json(
            TaxonomyError("TRAY_NOT_RUNNING", "QuestLog Tray is not running."),
            statusCode: StatusCodes.Status409Conflict);
    }

    return reply.Ok
        ? Results.StatusCode(successStatusCode)
        : Results.Json(
            TaxonomyError(reply.ErrorCode ?? TrayControlProtocol.CommandFailed, reply.Message ?? "The tray could not run the command."),
            statusCode: reply.ErrorCode == TrayControlProtocol.BadRequest
                ? StatusCodes.Status400BadRequest
                : StatusCodes.Status409Conflict);
}

/// <summary>Tray khong chay thi doc thang muc khoi dong cung Windows ma tray tu ghi.</summary>
static bool TrayAutoStartEnabled()
{
    if (!OperatingSystem.IsWindows()) return false;
    using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
    return key?.GetValue(TrayControlProtocol.AutoStartValueName) != null;
}

// Routes a Google account may use before it has a username (design §7).
// Changing username/email is excluded: the first username comes only from the claim.
static bool AllowedDuringOnboarding(HttpRequest request)
{
    var path = request.Path;
    if (path == "/api/auth/account/username" || path == "/api/auth/account/email-change") return false;
    if (path.StartsWithSegments("/api/auth")) return true;
    return HttpMethods.IsGet(request.Method) &&
           (path == "/api/settings" || path == "/api/capabilities");
}

/*
 * Chay thao tac nhay cam sau khi giu cho reauth ticket. Ticket chi bi tieu khi
 * thao tac thanh cong; loi nguoi dung sua duoc (trung username...) tra ticket lai
 * de ho khong phai xin ma moi.
 */
static async Task<IResult> WithReauthTicket(
    HttpContext context,
    ReauthTicketStore tickets,
    AuthAccountDto account,
    string accessToken,
    string? reauthTicket,
    Func<Task<IResult>> operation)
{
    if (string.IsNullOrWhiteSpace(reauthTicket))
    {
        return AuthErrorResult(400, "REAUTH_REQUIRED", "Confirm with the code sent to your email.");
    }

    var reservation = tickets.Reserve(
        reauthTicket,
        account.Id,
        SupabaseAuthGateway.SessionIdFromAccessToken(accessToken));
    if (reservation is null)
    {
        return AuthErrorResult(401, "REAUTH_INVALID", "The confirmation expired. Request a new code.");
    }

    try
    {
        return await operation();
    }
    catch (SupabaseAuthException exception)
    {
        if (exception.StatusCode is >= 400 and < 500 and not 401 and not 423)
        {
            tickets.Release(reservation);
        }
        return AuthErrorResult(exception.StatusCode, exception.Code, exception.Message);
    }
}

static bool IsValidUsername(string username) =>
    username.Length is >= 3 and <= 32 &&
    username.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '.' or '-');

static IResult InvalidUsernameResult() =>
    AuthErrorResult(
        400,
        "INVALID_USERNAME",
        "Username must be 3-32 characters using letters, numbers, dot, dash, or underscore.");

static bool IsEmailCode(string code) => code.Length is >= 6 and <= 10 && code.All(char.IsAsciiDigit);

static void SetOAuthFlowCookie(HttpContext context, string flowId)
{
    context.Response.Cookies.Append(
        OAuthFlowCookie,
        flowId,
        new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            // Lax: cookie van di kem khi Supabase redirect top-level GET ve callback.
            SameSite = SameSiteMode.Lax,
            Path = "/api/auth/google",
            MaxAge = GoogleOAuthFlowStore.Lifetime,
            IsEssential = true,
        });
}

static void ClearOAuthFlowCookie(HttpContext context)
{
    if (!context.Request.Cookies.ContainsKey(OAuthFlowCookie)) return;
    context.Response.Cookies.Delete(
        OAuthFlowCookie,
        new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/api/auth/google",
            IsEssential = true,
        });
}

// Trang tinh: khong script, khong token, khong tai nguyen ngoai.
static string GoogleCallbackPage(bool success, bool vietnamese)
{
    var (title, body) = (success, vietnamese) switch
    {
        (true, true) => ("Đã xác thực", "Bạn có thể đóng tab này và quay lại QuestLog."),
        (true, false) => ("You're signed in", "You can close this tab and return to QuestLog."),
        (false, true) => ("Chưa hoàn tất", "Quay lại QuestLog để thử đăng nhập bằng Google lần nữa."),
        (false, false) => ("Sign-in didn't finish", "Return to QuestLog to try Google sign-in again."),
    };
    var lang = vietnamese ? "vi" : "en";
    return $$"""
        <!doctype html>
        <html lang="{{lang}}">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>QuestLog — {{title}}</title>
        <style>
          :root { color-scheme: light dark; }
          body { margin: 0; min-height: 100vh; display: grid; place-items: center;
                 font: 16px/1.5 system-ui, sans-serif; background: #1f232b; color: #e8eaed; }
          main { max-width: 26rem; padding: 2rem; text-align: center; }
          h1 { margin: 0 0 .5rem; font-size: 1.5rem; }
          p { margin: 0; color: #aab0b9; }
        </style>
        </head>
        <body><main><h1>{{title}}</h1><p>{{body}}</p></main></body>
        </html>
        """;
}

static async Task WriteAuthError(HttpContext context, int statusCode, string code, string message)
{
    context.Response.StatusCode = statusCode;
    await context.Response.WriteAsJsonAsync(new { error = new { code, message } });
}

static void SetRefreshCookie(HttpContext context, string refreshToken)
{
    context.Response.Cookies.Append(
        "questlog_refresh_token",
        refreshToken,
        new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/api/auth",
            MaxAge = TimeSpan.FromDays(30),
            IsEssential = true,
        });
}

static void ClearRefreshCookie(HttpContext context)
{
    context.Response.Cookies.Delete(
        "questlog_refresh_token",
        new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/api/auth",
            IsEssential = true,
        });
}

static SettingsDto ToSettingsDto(Config config) => new(
    Language: config.Language,
    ThemeMode: config.ThemeMode,
    JungleFx: string.IsNullOrWhiteSpace(config.JungleFx) ? "auto" : config.JungleFx,
    JungleWeather: string.IsNullOrWhiteSpace(config.JungleWeather) ? "clear" : config.JungleWeather,
    AppMode: config.AppMode == "console" ? "console" : "desktop",
    SeenGuides: config.SeenGuides ?? new List<string>(),
    BackupRootPath: config.BackupRootPath,
    DatabasePath: config.DatabasePath,
    Libraries: new LibrarySettingsDto(
        Paths: config.GameLibraryPath,
        // Scanner capability is currently false, so there is no runtime snapshot to expose.
        Runtime: Array.Empty<LibraryRuntimeSettingsDto>()),
    Cloudinary: new CloudinarySettingsDto(
        CloudName: config.CloudinaryCloudName,
        Configured: !string.IsNullOrWhiteSpace(config.CloudinaryCloudName) &&
                    !string.IsNullOrWhiteSpace(config.CloudinaryApiKey) &&
                    !string.IsNullOrWhiteSpace(config.CloudinaryApiSecret)),
    Rawg: new RawgSettingsDto(Configured: false));

static Task<List<Game>> LoadQueueCandidates(MasterPlayContext db, string? ownerKey = null) => db.Games
    .Where(g => !g.IsDisabled && (ownerKey == null || g.LibraryOwnerKey == ownerKey))
    .Include(g => g.Progress)
    .Include(g => g.Ownership)
    .Include(g => g.Series)
    .Include(g => g.Finishes)
    .Include(g => g.GamePosition)
    .ToListAsync();

static Task<Game?> LoadGameDetail(MasterPlayContext db, string id, string? ownerKey = null) => db.Games
    .AsNoTracking()
    .Where(g => g.Id == id && !g.IsDisabled && (ownerKey == null || g.LibraryOwnerKey == ownerKey))
    .Include(g => g.Progress)
    .Include(g => g.Ownership)
    .Include(g => g.Series)
    .Include(g => g.GameGenres).ThenInclude(link => link.Genre)
    .Include(g => g.GameThemes).ThenInclude(link => link.Theme)
    .Include(g => g.Finishes)
    .Include(g => g.OwnershipInfo)
    .AsSplitQuery()
    .SingleOrDefaultAsync();

static async Task<GameDetailDto> ReloadGameDetailDto(MasterPlayContext db, string gameId, string? ownerKey = null)
{
    db.ChangeTracker.Clear();
    var game = await LoadGameDetail(db, gameId, ownerKey) ??
        throw new InvalidOperationException($"Saved game could not be reloaded: {gameId}");
    var baseGameName = game.BaseGameId is null
        ? null
        : await db.Games.AsNoTracking()
            .Where(g => g.Id == game.BaseGameId)
            .Select(g => g.Name)
            .FirstOrDefaultAsync();
    return ToGameDetailDto(game, baseGameName, (await LaunchTargets.LoadAsync(db)).CanLaunch(game));
}

static object TaxonomyError(string code, string message) => new { error = new { code, message } };

/// <summary>
/// Doc thoi diem hoan thanh tu than yeu cau.
///
/// Nam bo trong = dung thoi diem hien tai (hanh vi cu cua nut Finish). Thang bo
/// trong = nguoi choi chi nho nam: luu ngay 1 thang 1 nhung danh dau
/// <c>hasMonth = false</c> de UI khong bia ra mot thang 1 khong co that.
///
/// Chan nam tuong lai xa: mot so nam go nham (2206) se troi len dau moi bang
/// sort "finish time" va khong ai hieu tai sao.
/// </summary>
static bool TryReadFinishedAt(int? year, int? month, out DateTime finishedAt, out bool hasMonth)
{
    finishedAt = DateTime.Now;
    hasMonth = true;

    if (year is null)
        return month is null;

    if (year is < 1970 || year > DateTime.Now.Year + 1)
        return false;
    if (month is not null && month is < 1 or > 12)
        return false;

    hasMonth = month is not null;
    finishedAt = new DateTime(year.Value, month ?? 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
    return true;
}

static IResult InvalidFinishTimeResult() => Results.BadRequest(new
{
    error = new
    {
        code = "INVALID_FINISH_TIME",
        message = "Finish year must be a real year; month, when given, must be 1-12."
    }
});

static bool IsValidRating(double? value) =>
    value.HasValue &&
    (value.Value >= 0 && value.Value <= 11 &&
     Math.Abs(value.Value * 2 - Math.Round(value.Value * 2)) < 0.0001);

static async Task<string?> ValidateUserDataAsync(
    MasterPlayContext db,
    UserDataPatchDto body,
    CancellationToken cancellationToken)
{
    if (body.Score is not null && !IsValidRating(body.Score))
        return "Score must be between 0 and 11 in 0.5 steps.";
    if (body.Excited is not null && !IsValidRating(body.Excited))
        return "Excited must be between 0 and 11 in 0.5 steps.";
    if (body.TotalPlaytimeSeconds is < 0)
        return "Total playtime cannot be negative.";

    if (body.ProgressId.HasValue && !await db.ProgressStatuses.AnyAsync(item => item.Id == body.ProgressId.Value, cancellationToken))
        return "Progress reference does not exist.";
    if (body.OwnershipId.HasValue && !await db.OwnershipStatuses.AnyAsync(item => item.Id == body.OwnershipId.Value, cancellationToken))
        return "Ownership reference does not exist.";

    var launchPath = body.LaunchTarget?.Path?.Trim();
    if (!string.IsNullOrWhiteSpace(launchPath))
    {
        var extension = Path.GetExtension(launchPath).ToLowerInvariant();
        var expectedKind = extension switch { ".exe" => "exe", ".lnk" => "shortcut", _ => null };
        var requestedKind = body.LaunchTarget?.Kind?.Trim().ToLowerInvariant();
        if (!Path.IsPathFullyQualified(launchPath) || expectedKind is null || requestedKind != expectedKind)
            return "Launch target must be an absolute .exe or .lnk path with the matching kind.";
    }

    return null;
}

static UserDataPatchDto ToUserDataPatch(LibraryGameImportUserDataDto body) => new(
    body.Score,
    body.Excited,
    body.TotalPlaytimeSeconds,
    body.ProgressId,
    body.OwnershipId,
    body.ExeName,
    body.LaunchTarget,
    body.SaveFolderPath,
    body.BackupFolderPath,
    body.OwnershipInfo);

static void ApplyUserData(Game game, UserDataPatchDto body, MasterPlayContext db)
{
    static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    game.Score = body.Score;
    game.Excited = body.Excited;
    game.TotalPlayTimeSeconds = body.TotalPlaytimeSeconds;
    game.ProgressId = body.ProgressId;
    game.OwnershipId = body.OwnershipId;
    game.ExeName = Clean(body.ExeName);
    game.LaunchTargetKind = Clean(body.LaunchTarget?.Kind)?.ToLowerInvariant();
    game.LaunchTargetPath = Clean(body.LaunchTarget?.Path);
    if (game.LaunchTargetPath is null) game.LaunchTargetKind = null;
    game.SaveFolderPath = Clean(body.SaveFolderPath);
    game.BackupFolderPath = Clean(body.BackupFolderPath);

    var info = body.OwnershipInfo;
    var hasOwnershipInfo = info is not null && new[]
    {
        info.SteamAccount, info.SteamPassword, info.UbisoftAccount, info.UbisoftPassword,
        info.SupportDocumentUrl, info.DownloadUrl, info.Note,
    }.Any(value => !string.IsNullOrWhiteSpace(value));

    if (!hasOwnershipInfo)
    {
        if (game.OwnershipInfo is not null) db.GameOwnershipInfos.Remove(game.OwnershipInfo);
        game.OwnershipInfo = null;
    }
    else
    {
        game.OwnershipInfo ??= new GameOwnershipInfo { GameId = game.Id };
        game.OwnershipInfo.SteamAccount = Clean(info!.SteamAccount);
        game.OwnershipInfo.SteamPassword = Clean(info.SteamPassword);
        game.OwnershipInfo.UbisoftAccount = Clean(info.UbisoftAccount);
        game.OwnershipInfo.UbisoftPassword = Clean(info.UbisoftPassword);
        game.OwnershipInfo.SupportDocumentUrl = Clean(info.SupportDocumentUrl);
        game.OwnershipInfo.DownloadUrl = Clean(info.DownloadUrl);
        game.OwnershipInfo.Note = Clean(info.Note);
    }
}

static async Task<IResult> SaveGame(
    MasterPlayContext db,
    ImageAutoSyncQueue imageUploads,
    string? routeId,
    GameUpsertDto body,
    bool create,
    string ownerKey)
{
    var name = body.Name?.Trim();
    if (string.IsNullOrWhiteSpace(name))
    {
        return Results.BadRequest(new
        {
            error = new { code = "GAME_NAME_REQUIRED", message = "Game name is required." }
        });
    }

    Game game;
    if (create)
    {
        game = new Game { Id = Guid.NewGuid().ToString(), LibraryOwnerKey = ownerKey, CreateAt = DateTime.Now };
        db.Games.Add(game);
    }
    else
    {
        var existingGame = await db.Games
            .Include(g => g.GameGenres)
            .Include(g => g.GameThemes)
            .Include(g => g.OwnershipInfo)
            .Include(g => g.GamePosition)
            .SingleOrDefaultAsync(g => g.Id == routeId && g.LibraryOwnerKey == ownerKey && !g.IsDisabled);

        if (existingGame is null)
        {
            return Results.NotFound(new
            {
                error = new { code = "GAME_NOT_FOUND", message = $"Game not found: {routeId}" }
            });
        }
        game = existingGame;
    }

    static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    DateOnly? publishDate = null;
    if (!string.IsNullOrWhiteSpace(body.PublishDate))
    {
        if (!DateOnly.TryParse(body.PublishDate, out var parsedDate))
        {
            return Results.BadRequest(new
            {
                error = new { code = "INVALID_PUBLISH_DATE", message = "Publish date must use YYYY-MM-DD." }
            });
        }
        publishDate = parsedDate;
    }

    var progressId = body.Progress?.Id;
    if (create && progressId is null)
    {
        progressId = await FindProgressId(db, "Pending");
        if (progressId is null)
        {
            return Results.Json(new
            {
                error = new
                {
                    code = "PENDING_PROGRESS_MISSING",
                    message = "The Pending progress catalog entry is unavailable."
                }
            }, statusCode: StatusCodes.Status409Conflict);
        }
    }
    var ownershipId = body.Ownership?.Id;
    if (create && ownershipId is null)
    {
        ownershipId = await FindOwnershipId(db, "Wishlist");
        if (ownershipId is null)
        {
            return Results.Json(new
            {
                error = new
                {
                    code = "WISHLIST_OWNERSHIP_MISSING",
                    message = "The Wishlist ownership catalog entry is unavailable."
                }
            }, statusCode: StatusCodes.Status409Conflict);
        }
    }
    var seriesId = body.Series?.Id;
    var genreIds = (body.Genres ?? []).Select(item => item.Id).Distinct().ToArray();
    var themeIds = (body.Themes ?? []).Select(item => item.Id).Distinct().ToArray();

    if ((progressId.HasValue && !await db.ProgressStatuses.AnyAsync(x => x.Id == progressId.Value)) ||
        (ownershipId.HasValue && !await db.OwnershipStatuses.AnyAsync(x => x.Id == ownershipId.Value)) ||
        (seriesId.HasValue && !await db.Series.AnyAsync(x => x.Id == seriesId.Value)) ||
        genreIds.Length != await db.Genres.CountAsync(x => genreIds.Contains(x.Id)) ||
        themeIds.Length != await db.Themes.CountAsync(x => themeIds.Contains(x.Id)))
    {
        return Results.BadRequest(new
        {
            error = new { code = "INVALID_GAME_REFERENCE", message = "One or more selected catalog values no longer exist." }
        });
    }

    // Chi gan duoc tag chung hoac tag ca nhan cua chinh minh (spec 2026-10-04 §5.4).
    if (await db.Genres.AnyAsync(x => genreIds.Contains(x.Id) &&
            x.LibraryOwnerKey != TaxonomyRules.SharedOwnerKey && x.LibraryOwnerKey != ownerKey) ||
        await db.Themes.AnyAsync(x => themeIds.Contains(x.Id) &&
            x.LibraryOwnerKey != TaxonomyRules.SharedOwnerKey && x.LibraryOwnerKey != ownerKey))
    {
        return Results.BadRequest(new
        {
            error = new { code = "TAXONOMY_NOT_VISIBLE", message = "One or more selected genres or themes belong to another library." }
        });
    }

    static bool ValidRating(double? value) =>
        value is null || IsValidRating(value);

    if (!ValidRating(body.Score) || !ValidRating(body.Excited) ||
        body.MetaScore is < 0 or > 100 ||
        body.TotalPlaytimeSeconds is < 0 || body.AvgPlaytimeHours is < 0)
    {
        return Results.BadRequest(new
        {
            error = new
            {
                code = "INVALID_GAME_VALUE",
                message = "Ratings or playtime values are outside the supported range."
            }
        });
    }

    var baseGameId = Clean(body.BaseGameId);
    if (baseGameId == game.Id ||
        baseGameId is not null && !await db.Games.AnyAsync(x => x.Id == baseGameId && x.LibraryOwnerKey == ownerKey && !x.IsDisabled))
    {
        return Results.BadRequest(new
        {
            error = new { code = "INVALID_BASE_GAME", message = "The selected base game is invalid." }
        });
    }

    var launchKind = Clean(body.LaunchTarget?.Kind)?.ToLowerInvariant();
    var launchPath = Clean(body.LaunchTarget?.Path);
    if (launchPath is not null)
    {
        var extension = Path.GetExtension(launchPath).ToLowerInvariant();
        var expectedKind = extension switch { ".exe" => "exe", ".lnk" => "shortcut", _ => null };
        if (!Path.IsPathFullyQualified(launchPath) || expectedKind is null || launchKind != expectedKind)
        {
            return Results.BadRequest(new
            {
                error = new
                {
                    code = "INVALID_LAUNCH_TARGET",
                    message = "Launch target must be an absolute .exe or .lnk path with the matching kind."
                }
            });
        }
    }
    else
    {
        launchKind = null;
    }

    game.Name = name;
    game.Developer = Clean(body.Developer);
    game.Publisher = Clean(body.Publisher);
    game.SeriesId = seriesId;
    var previousImages = (game.BannerPath, game.BackdropPath, game.PosterPath);
    // Form tai truoc khi upload nen xong se gui lai link goc cu: giu ban Cloudinary.
    game.BannerPath =GameImageSources.KeepUploadedCopy(
        game.ImageSources, GameImageSources.Banner, game.BannerPath, Clean(body.BannerPath));
    game.BackdropPath = GameImageSources.KeepUploadedCopy(
        game.ImageSources, GameImageSources.Backdrop, game.BackdropPath, Clean(body.BackdropPath));
    game.PosterPath = GameImageSources.KeepUploadedCopy(
        game.ImageSources, GameImageSources.Poster, game.PosterPath, Clean(body.PosterPath));
    game.ProgressId = progressId;
    game.OwnershipId = ownershipId;
    game.MetaScore = body.MetaScore;
    game.Excited = body.Excited;
    game.Score = body.Score;
    game.TotalPlayTimeSeconds = body.TotalPlaytimeSeconds;
    game.AvgPlayTime = body.AvgPlaytimeHours;
    game.PublishDate = publishDate;
    game.IsDlc = body.IsDlc;
    game.SlugName = Clean(body.SlugName);
    game.ExeName = Clean(body.ExeName);
    game.LaunchTargetKind = launchKind;
    game.LaunchTargetPath = launchPath;
    game.SaveFolderPath = Clean(body.SaveFolderPath);
    game.BackupFolderPath = Clean(body.BackupFolderPath);
    game.BaseGameId = baseGameId;

    db.GameGenres.RemoveRange(game.GameGenres);
    game.GameGenres = genreIds
        .Select(genreId => new GameGenre { GameId = game.Id, GenreId = genreId })
        .ToList();
    db.GameThemes.RemoveRange(game.GameThemes);
    game.GameThemes = themeIds
        .Select(themeId => new GameTheme { GameId = game.Id, ThemeId = themeId })
        .ToList();

    var info = body.OwnershipInfo;
    var hasOwnershipInfo = info is not null && new[]
    {
        info.SteamAccount, info.SteamPassword, info.UbisoftAccount, info.UbisoftPassword,
        info.SupportDocumentUrl, info.DownloadUrl, info.Note,
    }.Any(value => !string.IsNullOrWhiteSpace(value));

    if (!hasOwnershipInfo)
    {
        if (game.OwnershipInfo is not null) db.GameOwnershipInfos.Remove(game.OwnershipInfo);
        game.OwnershipInfo = null;
    }
    else
    {
        game.OwnershipInfo ??= new GameOwnershipInfo { GameId = game.Id };
        game.OwnershipInfo.SteamAccount = Clean(info!.SteamAccount);
        game.OwnershipInfo.SteamPassword = Clean(info.SteamPassword);
        game.OwnershipInfo.UbisoftAccount = Clean(info.UbisoftAccount);
        game.OwnershipInfo.UbisoftPassword = Clean(info.UbisoftPassword);
        game.OwnershipInfo.SupportDocumentUrl = Clean(info.SupportDocumentUrl);
        game.OwnershipInfo.DownloadUrl = Clean(info.DownloadUrl);
        game.OwnershipInfo.Note = Clean(info.Note);
    }

    await db.SaveChangesAsync();
    // Chi khi o anh doi: luu diem so khong duoc thu lai mot link anh da chet.
    if (previousImages != (game.BannerPath, game.BackdropPath, game.PosterPath))
        imageUploads.EnqueueIfNeeded(game);

    db.ChangeTracker.Clear();
    var saved = await LoadGameDetail(db, game.Id, ownerKey);
    var baseGameName = saved?.BaseGameId is null
        ? null
        : await db.Games.AsNoTracking()
            .Where(g => g.Id == saved.BaseGameId)
            .Select(g => g.Name)
            .FirstOrDefaultAsync();

    var canLaunch = (await LaunchTargets.LoadAsync(db)).CanLaunch(saved!);
    return Results.Json(ToGameDetailDto(saved!, baseGameName, canLaunch), statusCode: create ? 201 : 200);
}

static async Task PersistQueuePositions(
    MasterPlayContext db,
    IReadOnlyList<Game> queue,
    bool saveChanges = true)
{
    var queueIds = queue.Select(g => g.Id).ToHashSet(StringComparer.Ordinal);
    var existing = await db.GamePositions.ToDictionaryAsync(p => p.GameId, StringComparer.Ordinal);

    foreach (var position in existing.Values.Where(p => !queueIds.Contains(p.GameId)))
    {
        db.GamePositions.Remove(position);
    }

    for (var index = 0; index < queue.Count; index++)
    {
        var game = queue[index];
        if (existing.TryGetValue(game.Id, out var position))
        {
            position.PlayOrder = index + 1;
            game.GamePosition = position;
        }
        else
        {
            var added = new GamePosition { GameId = game.Id, PlayOrder = index + 1 };
            db.GamePositions.Add(added);
            game.GamePosition = added;
        }
    }

    if (saveChanges) await db.SaveChangesAsync();
}


static string? ToIconDataUrl(string? iconData)
{
    if (string.IsNullOrWhiteSpace(iconData)) return null;

    var value = iconData.Trim();
    return value.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
        ? value
        : $"data:image/png;base64,{value}";
}

static async Task<int?> FindProgressId(
    MasterPlayContext db,
    string systemKey,
    CancellationToken cancellationToken = default)
{
    var items = await db.ProgressStatuses.AsNoTracking().ToListAsync(cancellationToken);
    return items.FirstOrDefault(item => LibraryQuery.ProgressSystemKey(item) == systemKey)?.Id;
}

static async Task<int?> FindOwnershipId(
    MasterPlayContext db,
    string systemKey,
    CancellationToken cancellationToken = default)
{
    var items = await db.OwnershipStatuses.AsNoTracking().ToListAsync(cancellationToken);
    return items.FirstOrDefault(item => LibraryQuery.OwnershipSystemKey(item) == systemKey)?.Id;
}

static CatalogItemDto? ToCatalog(ProgressStatus? p) => p is null ? null : new CatalogItemDto(
    Id: p.Id,
    SystemKey: LibraryQuery.ProgressSystemKey(p),
    Name: p.Name,
    // TODO(A20): localize theo Accept-Language. Tam thoi tra ten tu DB.
    DisplayName: p.Name,
    ColorCode: p.ColorCode ?? "#94a3b8",
    IconData: ToIconDataUrl(p.IconData),
    SortOrder: p.SortOrder);

static CatalogItemDto? ToCatalogOwn(OwnershipStatus? o) => o is null ? null : new CatalogItemDto(
    Id: o.Id,
    SystemKey: LibraryQuery.OwnershipSystemKey(o),
    Name: o.Name,
    DisplayName: o.Name,
    ColorCode: o.ColorCode ?? "#94a3b8",
    IconData: ToIconDataUrl(o.IconData),
    SortOrder: o.SortOrder,
    InfoLayout: LibraryQuery.OwnershipInfoLayout(o));

static GameListItemDto ToDto(Game g, bool canLaunch)
{
    return new GameListItemDto(
        Id: g.Id,
        MetadataGameId: g.MetadataGameId,
        MetadataRevision: g.MetadataRevision,
        MetadataSyncedAt: g.MetadataSyncedAt,
        IsMetadataDerived: g.MetadataGameId.HasValue,
        Name: g.Name,
        Developer: g.Developer,
        Publisher: g.Publisher,
        Series: g.Series is null ? null : new TaxonomyItemDto(g.Series.Id, g.Series.Name, null, 0),
        BannerPath: g.BannerPath,
        PosterPath: g.PosterPath,
        BackdropPath: g.BackdropPath,
        Progress: ToCatalog(g.Progress),
        Ownership: ToCatalogOwn(g.Ownership),
        MetaScore: g.MetaScore,
        MetaScoreBand: LibraryQuery.MetaBand(g.MetaScore),
        Excited: g.Excited,
        Score: g.Score,
        ScoreMaterialBand: LibraryQuery.ScoreMaterialBand(g.Score),
        TotalPlaytimeSeconds: g.TotalPlayTimeSeconds,
        AvgPlaytimeHours: g.AvgPlayTime,
        AvgPlaytimeBand: LibraryQuery.AvgBand(g.AvgPlayTime),
        PublishDate: g.PublishDate?.ToString("yyyy-MM-dd"),
        LastFinishedAt: LibraryQuery.LastFinishedAt(g),
        LastPlayedAt: g.LastPlayedAt.HasValue
            ? DateTime.SpecifyKind(g.LastPlayedAt.Value, DateTimeKind.Utc)
            : null,
        IsDlc: g.IsDlc,
        IsUpcoming: g.PublishDate.HasValue &&
            g.PublishDate.Value > DateOnly.FromDateTime(DateTime.Now),
        Ready: g.Ready,
        // TODO(A11): lay tu GameProcessMonitor qua SSE.
        IsRunning: false,
        CanLaunch: canLaunch,
        // TODO(A10): tinh theo GameViewModel.CheckFolderWarning.
        FolderWarning: null,
        // Ten base game duoc resolve o endpoint list (tu chinh trang thu vien da nap).
        BaseGameId: g.BaseGameId);
}

static GameDetailDto ToGameDetailDto(Game g, string? baseGameName, bool canLaunch)
{
    var list = ToDto(g, canLaunch);
    return new GameDetailDto(
        Id: list.Id,
        MetadataGameId: list.MetadataGameId,
        MetadataRevision: list.MetadataRevision,
        MetadataSyncedAt: list.MetadataSyncedAt,
        IsMetadataDerived: list.IsMetadataDerived,
        Name: list.Name,
        Developer: list.Developer,
        Publisher: list.Publisher,
        Series: list.Series,
        BannerPath: list.BannerPath,
        PosterPath: list.PosterPath,
        BackdropPath: list.BackdropPath,
        Progress: list.Progress,
        Ownership: list.Ownership,
        MetaScore: list.MetaScore,
        MetaScoreBand: list.MetaScoreBand,
        Excited: list.Excited,
        Score: list.Score,
        ScoreMaterialBand: list.ScoreMaterialBand,
        TotalPlaytimeSeconds: list.TotalPlaytimeSeconds,
        AvgPlaytimeHours: list.AvgPlaytimeHours,
        AvgPlaytimeBand: list.AvgPlaytimeBand,
        PublishDate: list.PublishDate,
        LastFinishedAt: list.LastFinishedAt,
        LastPlayedAt: list.LastPlayedAt,
        IsDlc: list.IsDlc,
        IsUpcoming: list.IsUpcoming,
        Ready: list.Ready,
        IsRunning: list.IsRunning,
        CanLaunch: list.CanLaunch,
        FolderWarning: list.FolderWarning,
        SlugName: g.SlugName,
        ExeName: g.ExeName,
        LaunchTarget: string.IsNullOrWhiteSpace(g.LaunchTargetPath)
            ? null
            : new LaunchTargetDto(g.LaunchTargetKind ?? "exe", g.LaunchTargetPath),
        SaveFolderPath: g.SaveFolderPath,
        BackupFolderPath: g.BackupFolderPath,
        Genres: g.GameGenres.OrderBy(link => link.Genre.SortOrder)
            .ThenBy(link => link.Genre.Name)
            .Select(link => TaxonomyEndpoints.ToDto(link.Genre, g.LibraryOwnerKey))
            .ToList(),
        Themes: g.GameThemes.OrderBy(link => link.Theme.SortOrder)
            .ThenBy(link => link.Theme.Name)
            .Select(link => TaxonomyEndpoints.ToDto(link.Theme, g.LibraryOwnerKey))
            .ToList(),
        BaseGameId: g.BaseGameId,
        BaseGameName: baseGameName,
        Finishes: g.Finishes.OrderByDescending(f => f.FinishedAt)
            .Select(f => new GameFinishEntryDto(f.Id, f.FinishedAt, f.HasMonth, f.Score))
            .ToList(),
        OwnershipInfo: g.OwnershipInfo is null
            ? null
            : new OwnershipInfoDto(
                g.OwnershipInfo.SteamAccount,
                g.OwnershipInfo.SteamPassword,
                g.OwnershipInfo.UbisoftAccount,
                g.OwnershipInfo.UbisoftPassword,
                g.OwnershipInfo.SupportDocumentUrl,
                g.OwnershipInfo.DownloadUrl,
                g.OwnershipInfo.Note),
        CreateAt: g.CreateAt);
}
