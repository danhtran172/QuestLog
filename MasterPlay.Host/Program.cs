using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Globalization;
using System.Net.Mail;
using MasterPlay.Core.Entities;
using MasterPlay.Infrastructure.Data;
using MasterPlay.Infrastructure.Data.Services;
using MasterPlay.Infrastructure.Configuration;
using MasterPlay.Infrastructure.Integrations.Gemini;
using MasterPlay.Infrastructure.Integrations.Igdb;
using MasterPlay.Infrastructure.Integrations.Igdb.Models;
using MasterPlay.Infrastructure.Integrations.Rawg;
using MasterPlay.Host;
using MasterPlay.Host.Auth;
using MasterPlay.Host.Metadata;
using System.Threading.RateLimiting;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Local desktop setup: read secrets once from the per-user AppData folder.
// Environment variables remain supported and take precedence for deployment/CI.
var authConfigDirectory = AppPaths.GetDataDirectory();
builder.Configuration.AddJsonFile(
    new PhysicalFileProvider(authConfigDirectory),
    "auth.local.json",
    optional: true,
    reloadOnChange: false);
// WebApplicationBuilder registers environment variables before custom JSON
// providers. Register them once more so an explicitly supplied deployment
// secret always wins over a stale per-user auth.local.json value.
builder.Configuration.AddEnvironmentVariables();

DataService.EnsureInitialized();

builder.Services.AddDbContext<MasterPlayContext>();

var authOptions = SupabaseAuthOptions.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(authOptions);
builder.Services.AddSingleton<DesktopGuestSession>();
builder.Services.AddHttpClient(nameof(SupabaseAuthGateway), client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddHttpClient(nameof(SupabaseMetadataCatalog), client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddSingleton<SupabaseAuthGateway>();
builder.Services.AddSingleton<SupabaseMetadataCatalog>();
builder.Services.AddHostedService<BootstrapAdminService>();
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
                   path == "/api/auth/guest" ||
                   path == "/api/auth/session" ||
                   path == "/api/auth/logout";

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
        if (string.Equals(account.Role, "admin", StringComparison.OrdinalIgnoreCase))
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

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", db = JsonService.GetDatabasePath() }));

app.MapGet("/api/auth/config", () => Results.Ok(new AuthConfigDto(
    authOptions.IsConfigured,
    authOptions.RegistrationEnabled,
    authOptions.GoogleEnabled)));

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

app.MapPost("/api/auth/change-password", async (
    HttpContext context,
    SupabaseAuthGateway gateway,
    ChangePasswordRequestDto body,
    CancellationToken cancellationToken) =>
{
    var currentPassword = body.CurrentPassword ?? string.Empty;
    var newPassword = body.NewPassword ?? string.Empty;
    if (string.IsNullOrWhiteSpace(currentPassword) || newPassword.Length < 12)
    {
        return AuthErrorResult(
            400,
            "INVALID_PASSWORD_CHANGE",
            "Enter your current password and a new password with at least 12 characters.");
    }

    if (string.Equals(currentPassword, newPassword, StringComparison.Ordinal))
    {
        return AuthErrorResult(400, "PASSWORD_UNCHANGED", "Choose a different password.");
    }

    if (context.Items["QuestLogAccount"] is not AuthAccountDto account)
    {
        return AuthErrorResult(401, "AUTH_REQUIRED", "Sign in to continue.");
    }

    var accessToken = BearerToken(context.Request);
    if (string.IsNullOrWhiteSpace(accessToken))
    {
        return AuthErrorResult(401, "AUTH_REQUIRED", "Sign in to continue.");
    }

    try
    {
        var result = await gateway.ChangePasswordAsync(
            account,
            accessToken,
            currentPassword,
            newPassword,
            cancellationToken);
        SetRefreshCookie(context, result.RefreshToken);
        return Results.Ok(result.Session);
    }
    catch (SupabaseAuthException exception)
    {
        return AuthErrorResult(exception.StatusCode, exception.Code, exception.Message);
    }
}).RequireRateLimiting("auth");

app.MapPatch("/api/auth/account/username", async (
    HttpContext context,
    SupabaseAuthGateway gateway,
    ChangeUsernameRequestDto body,
    CancellationToken cancellationToken) =>
{
    var username = body.Username?.Trim() ?? string.Empty;
    var currentPassword = body.CurrentPassword ?? string.Empty;
    if (username.Length is < 3 or > 32 ||
        username.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '_' or '.' or '-')))
    {
        return AuthErrorResult(
            400,
            "INVALID_USERNAME",
            "Username must be 3-32 characters using letters, numbers, dot, dash, or underscore.");
    }

    if (string.IsNullOrWhiteSpace(currentPassword))
    {
        return AuthErrorResult(400, "CURRENT_PASSWORD_REQUIRED", "Enter your current password.");
    }

    if (context.Items["QuestLogAccount"] is not AuthAccountDto account)
    {
        return AuthErrorResult(401, "AUTH_REQUIRED", "Sign in to continue.");
    }

    try
    {
        return Results.Ok(await gateway.ChangeUsernameAsync(
            account,
            currentPassword,
            username,
            cancellationToken));
    }
    catch (SupabaseAuthException exception)
    {
        return AuthErrorResult(exception.StatusCode, exception.Code, exception.Message);
    }
}).RequireRateLimiting("auth");

app.MapPost("/api/auth/account/email-change", async (
    HttpContext context,
    SupabaseAuthGateway gateway,
    ChangeEmailRequestDto body,
    CancellationToken cancellationToken) =>
{
    var email = body.Email?.Trim() ?? string.Empty;
    var currentPassword = body.CurrentPassword ?? string.Empty;
    if (!MailAddress.TryCreate(email, out var parsedEmail) ||
        !string.Equals(parsedEmail.Address, email, StringComparison.OrdinalIgnoreCase))
    {
        return AuthErrorResult(400, "INVALID_EMAIL", "Enter a valid email address.");
    }

    if (string.IsNullOrWhiteSpace(currentPassword))
    {
        return AuthErrorResult(400, "CURRENT_PASSWORD_REQUIRED", "Enter your current password.");
    }

    if (context.Items["QuestLogAccount"] is not AuthAccountDto account)
    {
        return AuthErrorResult(401, "AUTH_REQUIRED", "Sign in to continue.");
    }

    if (string.Equals(account.Email, email, StringComparison.OrdinalIgnoreCase))
    {
        return AuthErrorResult(400, "EMAIL_UNCHANGED", "Choose a different email address.");
    }

    var accessToken = BearerToken(context.Request);
    if (string.IsNullOrWhiteSpace(accessToken))
    {
        return AuthErrorResult(401, "AUTH_REQUIRED", "Sign in to continue.");
    }

    try
    {
        return Results.Ok(await gateway.ChangeEmailAsync(
            account,
            accessToken,
            currentPassword,
            email,
            cancellationToken));
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
        await gateway.LogoutAsync(accessToken, cancellationToken);
    }

    ClearRefreshCookie(context);
    return Results.NoContent();
});

app.MapGet("/api/capabilities", () => Results.Ok(new CapabilitiesDto(
    // Local API chay tren may co game -> co day du kha nang native.
    // Cac endpoint native chua cai dat, se bo sung dan (xem UI_MAP.md §6).
    Mode: "local",
    Features: new CapabilityFeaturesDto(
        Launch: OperatingSystem.IsWindows(),
        FilePicker: OperatingSystem.IsWindows(),
        RevealFolder: OperatingSystem.IsWindows(),
        Tray: false,
        Scanner: false,
        Controller: OperatingSystem.IsWindows()))));

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

        foreach (var item in newItems)
        {
            var source = metadata[item.MetadataGameId];
            var game = new Game
            {
                Id = Guid.NewGuid().ToString(),
                LibraryOwnerKey = ownerKey,
                MetadataGameId = source.Id,
                MetadataRevision = source.Revision,
                MetadataSyncedAt = DateTime.UtcNow,
                Name = source.Name,
                Developer = source.Developer,
                Publisher = source.Publisher,
                PublishDate = source.PublishDate,
                IsDlc = source.IsDlc,
                GImagePath = source.GalleryImageUrl,
                ImagePath = source.CoverImageUrl,
                SteamLibraryImagePath = source.SteamLibraryImageUrl,
                MetaScore = source.MetaScore,
                AvgPlayTime = source.AvgPlaytimeHours,
                CreateAt = DateTime.Now,
            };

            if (item.UserData is not null)
                ApplyUserData(game, ToUserDataPatch(item.UserData), db);
            game.ProgressId ??= defaultProgressId;
            game.OwnershipId ??= defaultOwnershipId;

            db.Games.Add(game);
            results.Add(new LibraryGameImportResultItemDto(item.MetadataGameId, "imported", game.Id, null));
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

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

    IEnumerable<Game> result = LibraryQuery.ApplyTab(games, tab);
    if (tab == "queue")
    {
        // Queue owns its order and ignores library search/filter/sort controls.
        result = LibraryQuery.ApplySort(result, "default", "queue");
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
    if (tab != "queue" && reverseSort == true) list.Reverse();

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
            var dto = ToDto(g);
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
        ActiveFilterCount: tab == "queue" ? 0 : filter.ActiveCount,
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

    return Results.Ok(ToGameDetailDto(game, baseGameName));
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

app.MapPut("/api/games/{id}", async (HttpContext context, MasterPlayContext db, string id, GameUpsertDto body) =>
{
    if (!TryGetLibraryOwnerKey(context, out var ownerKey))
        return AuthErrorResult(401, "AUTH_REQUIRED", "Sign in or continue as guest to update games.");
    return await SaveGame(db, id, body, create: false, ownerKey);
});

app.MapPost("/api/games", async (HttpContext context, MasterPlayContext db, GameUpsertDto body) =>
{
    if (context.Items["QuestLogAccount"] is not AuthAccountDto account)
        return AuthErrorResult(403, "ADMIN_REQUIRED", "Only an administrator can create manual games.");
    if (!string.Equals(account.Role, "admin", StringComparison.OrdinalIgnoreCase))
        return AuthErrorResult(403, "ADMIN_REQUIRED", "Only an administrator can create manual games.");
    return await SaveGame(db, null, body, create: true, $"user:{account.Id}");
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

    var launchPath = game.LaunchTargetPath;
    if (string.IsNullOrWhiteSpace(launchPath) && !string.IsNullOrWhiteSpace(game.ExeName))
    {
        var processName = game.ExeName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? game.ExeName
            : game.ExeName + ".exe";
        launchPath = await db.DetectedProcesses.AsNoTracking()
            .Where(process => process.ExeName.ToLower() == processName.ToLower())
            .Select(process => process.FullPath)
            .FirstOrDefaultAsync();
    }

    if (string.IsNullOrWhiteSpace(launchPath))
        return Results.NotFound(TaxonomyError("LAUNCH_TARGET_NOT_FOUND", "No launch target or detected process is available."));
    if (!File.Exists(launchPath))
        return Results.NotFound(TaxonomyError("LAUNCH_FILE_NOT_FOUND", $"Launch file not found: {launchPath}"));

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

app.MapPost("/api/games/{id}/sync/{source}", async (
    HttpContext context,
    MasterPlayContext db,
    string id,
    string source,
    CancellationToken cancellationToken) =>
{
    if (!TryGetLibraryOwnerKey(context, out var ownerKey))
        return AuthErrorResult(401, "AUTH_REQUIRED", "Sign in or continue as guest to sync game metadata.");
    if (source is not ("metadata" or "cover" or "gallery" or "steam-library"))
        return Results.BadRequest(TaxonomyError("INVALID_SYNC_SOURCE", $"Unsupported sync source: {source}"));

    var game = await db.Games.SingleOrDefaultAsync(g => g.Id == id && g.LibraryOwnerKey == ownerKey && !g.IsDisabled, cancellationToken);
    if (game is null)
        return Results.NotFound(TaxonomyError("GAME_NOT_FOUND", $"Game not found: {id}"));

    try
    {
        if (source == "metadata")
        {
            // IGDB is optional for the general sync. A user who has not
            // connected Twitch credentials can still refresh RAWG/Steam data;
            // only the dedicated portrait-cover sync requires IGDB.
            var igdbTask = IgdbService.IsConfigured
                ? IgdbService.GetMetadataAsync(game.Name, cancellationToken)
                : Task.FromResult<IgdbMetadata?>(null);
            var rawgTask = RawgService.GetMetadata_Rawg(game.Name);
            var galleryTask = RawgService.GetGalleryImage(game.Name);
            var steamTask = RawgService.GetSteamLibraryImage(game.Name);
            var executableTask = GeminiService.GetExecutableNameAsync(game.Name, cancellationToken);
            await Task.WhenAll(new Task[] { igdbTask, rawgTask, galleryTask, steamTask, executableTask });

            var igdb = igdbTask.Result;
            var rawg = rawgTask.Result;
            if (igdb is null && rawg is null)
                return Results.Json(
                    TaxonomyError("METADATA_NOT_FOUND", $"No IGDB or RAWG entry found for {game.Name}."),
                    statusCode: StatusCodes.Status502BadGateway);

            if (igdb is not null)
            {
                // IGDB remains the preferred canonical source when configured.
                await IgdbMetadataSync.ApplyAsync(db, game, igdb, cancellationToken);
            }

            if (rawg is not null)
            {
                // When IGDB is not configured, RAWG supplies a useful
                // metadata fallback instead of making every Sync fail.
                if (igdb is null && !string.IsNullOrWhiteSpace(rawg.Name)) game.Name = rawg.Name.Trim();
                if (igdb is null && !string.IsNullOrWhiteSpace(rawg.Developers)) game.Developer = rawg.Developers.Trim();
                if (igdb is null && !string.IsNullOrWhiteSpace(rawg.Publishers)) game.Publisher = rawg.Publishers.Trim();
                if (igdb is null && DateOnly.TryParse(rawg.ReleaseDate, out var releaseDate)) game.PublishDate = releaseDate;
                if (!string.IsNullOrWhiteSpace(rawg.SlugName)) game.SlugName = rawg.SlugName.Trim();
                if (rawg.MetaScore is > 0) game.MetaScore = rawg.MetaScore;
                if (rawg.AvgPlaytime is > 0) game.AvgPlayTime = rawg.AvgPlaytime;
                // RAWG is the detail-cover source. IGDB's portrait is kept
                // separate as part of the three-image-role migration.
                if (!string.IsNullOrWhiteSpace(rawg.Background_Image)) game.ImagePath = rawg.Background_Image;
            }

            if (!string.IsNullOrWhiteSpace(galleryTask.Result)) game.GImagePath = galleryTask.Result;
            if (!string.IsNullOrWhiteSpace(steamTask.Result)) game.SteamLibraryImagePath = steamTask.Result;
            if (!string.IsNullOrWhiteSpace(executableTask.Result)) game.ExeName = executableTask.Result;
        }
        else if (source == "cover")
        {
            // Detail cover comes from RAWG and intentionally does not require
            // an IGDB/Twitch setup.
            var rawgCover = await RawgService.GetMetadata_Rawg(game.Name);
            if (rawgCover is null || string.IsNullOrWhiteSpace(rawgCover.Background_Image))
                return Results.Json(TaxonomyError("RAWG_COVER_NOT_FOUND", $"No RAWG cover found for {game.Name}."),
                    statusCode: StatusCodes.Status502BadGateway);

            game.ImagePath = rawgCover.Background_Image;
        }
        else if (source == "gallery")
        {
            var path = await RawgService.GetGalleryImage(game.Name);
            if (string.IsNullOrWhiteSpace(path))
                return Results.Json(TaxonomyError("GALLERY_NOT_FOUND", $"No gallery image found for {game.Name}."),
                    statusCode: StatusCodes.Status502BadGateway);
            game.GImagePath = path;
        }
        else
        {
            var path = await RawgService.GetSteamLibraryImage(game.Name);
            if (string.IsNullOrWhiteSpace(path))
                return Results.Json(TaxonomyError("STEAM_IMAGE_NOT_FOUND", $"No Steam Library image found for {game.Name}."),
                    statusCode: StatusCodes.Status502BadGateway);
            game.SteamLibraryImagePath = path;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(await ReloadGameDetailDto(db, id, ownerKey));
    }
    catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
    {
        return Results.Json(TaxonomyError("UPSTREAM_SYNC_FAILED", exception.Message),
            statusCode: StatusCodes.Status502BadGateway);
    }
});

app.MapPost("/api/games/{gameId}/finishes", async (
    HttpContext context,
    MasterPlayContext db,
    string gameId,
    FinishGameRequestDto body) =>
{
    if (!TryGetLibraryOwnerKey(context, out var ownerKey))
        return AuthErrorResult(401, "AUTH_REQUIRED", "Sign in or continue as guest to update finish history.");
    if (!IsValidRating(body.Score))
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

    await using var transaction = await db.Database.BeginTransactionAsync();
    var score = body.Score!.Value;
    game.Score = score;
    game.ProgressId = done.Id;
    db.GameFinishes.Add(new GameFinish
    {
        GameId = game.Id,
        FinishedAt = DateTime.Now,
        Score = score,
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
    if (string.IsNullOrWhiteSpace(body.FinishedMonth) ||
        !DateTime.TryParseExact(
            body.FinishedMonth,
            "yyyy-MM",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsedMonth))
    {
        return Results.BadRequest(new
        {
            error = new
            {
                code = "INVALID_FINISH_MONTH",
                message = "Finish month must use YYYY-MM."
            }
        });
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

    finish.FinishedAt = new DateTime(parsedMonth.Year, parsedMonth.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
    await db.SaveChangesAsync();

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
    return Results.Ok(queue.Select(ToDto).ToList());
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

    return Results.Ok(reordered.Select(ToDto).ToList());
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

app.MapGet("/api/taxonomy/{kind}", async (MasterPlayContext db, string kind, string? q) => kind switch
{
    "series" => Results.Ok(await db.Series.AsNoTracking()
        .Where(s => string.IsNullOrWhiteSpace(q) || s.Name.Contains(q))
        .OrderBy(s => s.Name)
        // Series chua co IconData/SortOrder trong DB — xem thay doi schema S2, S3.
        .Select(s => new TaxonomyItemDto(s.Id, s.Name, null, 0)).ToListAsync()),
    "genres" => Results.Ok(await db.Genres.AsNoTracking()
        .Where(g => string.IsNullOrWhiteSpace(q) || g.Name.Contains(q))
        .OrderBy(g => g.SortOrder).ThenBy(g => g.Name)
        .Select(g => new TaxonomyItemDto(g.Id, g.Name, g.IconData, g.SortOrder)).ToListAsync()),
    "themes" => Results.Ok(await db.Themes.AsNoTracking()
        .Where(x => string.IsNullOrWhiteSpace(q) || x.Name.Contains(q))
        .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
        .Select(x => new TaxonomyItemDto(x.Id, x.Name, x.IconData, x.SortOrder)).ToListAsync()),
    _ => Results.BadRequest(new { error = new { code = "BAD_KIND", message = $"kind khong hop le: {kind}" } }),
});

app.MapPost("/api/taxonomy/{kind}", async (
    MasterPlayContext db,
    string kind,
    TaxonomyItemDto body) => await CreateTaxonomyItem(db, kind, body));

app.MapPut("/api/taxonomy/{kind}/{id:int}", async (
    MasterPlayContext db,
    string kind,
    int id,
    TaxonomyItemDto body) => await UpdateTaxonomyItem(db, kind, id, body));

app.MapDelete("/api/taxonomy/{kind}/{id:int}", async (
    MasterPlayContext db,
    string kind,
    int id) => await DeleteTaxonomyItem(db, kind, id));

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
            imagePath = g.GImagePath,
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
            imagePath = g.GImagePath,
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
        missingGalleryImage = games.Count(g => string.IsNullOrWhiteSpace(g.GImagePath)),
        missingCoverImage = games.Count(g => string.IsNullOrWhiteSpace(g.ImagePath)),
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
        CoverImageUrl: game.CoverImageUrl,
        GalleryImageUrl: game.GalleryImageUrl,
        SteamLibraryImageUrl: game.SteamLibraryImageUrl,
        MetaScore: game.MetaScore,
        AvgPlaytimeHours: game.AvgPlaytimeHours,
        SeriesId: game.SeriesId,
        Revision: game.Revision,
        LibraryStatus: libraryStatus);

static IResult AuthErrorResult(int statusCode, string code, string message) =>
    Results.Json(new { error = new { code, message } }, statusCode: statusCode);

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
    return ToGameDetailDto(game, baseGameName);
}

static string? CleanTaxonomyName(string? value) =>
    string.IsNullOrWhiteSpace(value) ? null : value.Trim();

static object TaxonomyError(string code, string message) => new { error = new { code, message } };

static async Task<IResult> CreateTaxonomyItem(MasterPlayContext db, string kind, TaxonomyItemDto body)
{
    var name = CleanTaxonomyName(body.Name);
    if (name is null) return Results.BadRequest(TaxonomyError("TAXONOMY_NAME_REQUIRED", "Name is required."));

    if (kind == "series")
    {
        if (await db.Series.AnyAsync(x => x.Name.ToLower() == name.ToLower()))
            return Results.Conflict(TaxonomyError("TAXONOMY_NAME_EXISTS", $"Series already exists: {name}"));
        var item = new Series { Name = name };
        db.Series.Add(item);
        await db.SaveChangesAsync();
        return Results.Created($"/api/taxonomy/series/{item.Id}", new TaxonomyItemDto(item.Id, item.Name, null, 0));
    }

    if (kind == "genres")
    {
        if (await db.Genres.AnyAsync(x => x.Name.ToLower() == name.ToLower()))
            return Results.Conflict(TaxonomyError("TAXONOMY_NAME_EXISTS", $"Genre already exists: {name}"));
        var item = new Genre { Name = name, IconData = body.IconData, SortOrder = body.SortOrder };
        db.Genres.Add(item);
        await db.SaveChangesAsync();
        return Results.Created($"/api/taxonomy/genres/{item.Id}", new TaxonomyItemDto(item.Id, item.Name, item.IconData, item.SortOrder));
    }

    if (kind == "themes")
    {
        if (await db.Themes.AnyAsync(x => x.Name.ToLower() == name.ToLower()))
            return Results.Conflict(TaxonomyError("TAXONOMY_NAME_EXISTS", $"Theme already exists: {name}"));
        var item = new Theme { Name = name, IconData = body.IconData, SortOrder = body.SortOrder };
        db.Themes.Add(item);
        await db.SaveChangesAsync();
        return Results.Created($"/api/taxonomy/themes/{item.Id}", new TaxonomyItemDto(item.Id, item.Name, item.IconData, item.SortOrder));
    }

    return Results.BadRequest(TaxonomyError("BAD_KIND", $"kind khong hop le: {kind}"));
}

static async Task<IResult> UpdateTaxonomyItem(
    MasterPlayContext db,
    string kind,
    int id,
    TaxonomyItemDto body)
{
    var name = CleanTaxonomyName(body.Name);
    if (name is null) return Results.BadRequest(TaxonomyError("TAXONOMY_NAME_REQUIRED", "Name is required."));

    if (kind == "series")
    {
        var item = await db.Series.SingleOrDefaultAsync(x => x.Id == id);
        if (item is null) return Results.NotFound(TaxonomyError("TAXONOMY_NOT_FOUND", $"Series not found: {id}"));
        if (await db.Series.AnyAsync(x => x.Id != id && x.Name.ToLower() == name.ToLower()))
            return Results.Conflict(TaxonomyError("TAXONOMY_NAME_EXISTS", $"Series already exists: {name}"));
        item.Name = name;
        await db.SaveChangesAsync();
        return Results.Ok(new TaxonomyItemDto(item.Id, item.Name, null, 0));
    }

    if (kind == "genres")
    {
        var item = await db.Genres.SingleOrDefaultAsync(x => x.Id == id);
        if (item is null) return Results.NotFound(TaxonomyError("TAXONOMY_NOT_FOUND", $"Genre not found: {id}"));
        if (await db.Genres.AnyAsync(x => x.Id != id && x.Name.ToLower() == name.ToLower()))
            return Results.Conflict(TaxonomyError("TAXONOMY_NAME_EXISTS", $"Genre already exists: {name}"));
        item.Name = name;
        item.IconData = body.IconData;
        item.SortOrder = body.SortOrder;
        await db.SaveChangesAsync();
        return Results.Ok(new TaxonomyItemDto(item.Id, item.Name, item.IconData, item.SortOrder));
    }

    if (kind == "themes")
    {
        var item = await db.Themes.SingleOrDefaultAsync(x => x.Id == id);
        if (item is null) return Results.NotFound(TaxonomyError("TAXONOMY_NOT_FOUND", $"Theme not found: {id}"));
        if (await db.Themes.AnyAsync(x => x.Id != id && x.Name.ToLower() == name.ToLower()))
            return Results.Conflict(TaxonomyError("TAXONOMY_NAME_EXISTS", $"Theme already exists: {name}"));
        item.Name = name;
        item.IconData = body.IconData;
        item.SortOrder = body.SortOrder;
        await db.SaveChangesAsync();
        return Results.Ok(new TaxonomyItemDto(item.Id, item.Name, item.IconData, item.SortOrder));
    }

    return Results.BadRequest(TaxonomyError("BAD_KIND", $"kind khong hop le: {kind}"));
}

static async Task<IResult> DeleteTaxonomyItem(MasterPlayContext db, string kind, int id)
{
    await using var transaction = await db.Database.BeginTransactionAsync();
    if (kind == "series")
    {
        var item = await db.Series.SingleOrDefaultAsync(x => x.Id == id);
        if (item is null) return Results.NotFound(TaxonomyError("TAXONOMY_NOT_FOUND", $"Series not found: {id}"));
        await db.Games.Where(x => x.SeriesId == id).ExecuteUpdateAsync(update => update.SetProperty(x => x.SeriesId, (int?)null));
        db.Series.Remove(item);
    }
    else if (kind == "genres")
    {
        var item = await db.Genres.SingleOrDefaultAsync(x => x.Id == id);
        if (item is null) return Results.NotFound(TaxonomyError("TAXONOMY_NOT_FOUND", $"Genre not found: {id}"));
        await db.GameGenres.Where(x => x.GenreId == id).ExecuteDeleteAsync();
        db.Genres.Remove(item);
    }
    else if (kind == "themes")
    {
        var item = await db.Themes.SingleOrDefaultAsync(x => x.Id == id);
        if (item is null) return Results.NotFound(TaxonomyError("TAXONOMY_NOT_FOUND", $"Theme not found: {id}"));
        await db.GameThemes.Where(x => x.ThemeId == id).ExecuteDeleteAsync();
        db.Themes.Remove(item);
    }
    else
    {
        return Results.BadRequest(TaxonomyError("BAD_KIND", $"kind khong hop le: {kind}"));
    }

    await db.SaveChangesAsync();
    await transaction.CommitAsync();
    return Results.NoContent();
}

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
        baseGameId is not null && !await db.Games.AnyAsync(x => x.Id == baseGameId && !x.IsDisabled))
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
    game.GImagePath = Clean(body.GImagePath);
    game.ImagePath = Clean(body.ImagePath);
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
    game.SteamLibraryImagePath = Clean(body.SteamLibraryImagePath);
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

    db.ChangeTracker.Clear();
    var saved = await LoadGameDetail(db, game.Id, ownerKey);
    var baseGameName = saved?.BaseGameId is null
        ? null
        : await db.Games.AsNoTracking()
            .Where(g => g.Id == saved.BaseGameId)
            .Select(g => g.Name)
            .FirstOrDefaultAsync();

    return Results.Json(ToGameDetailDto(saved!, baseGameName), statusCode: create ? 201 : 200);
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
    SortOrder: o.SortOrder);

static GameListItemDto ToDto(Game g)
{
    // Nut Play chi hien khi that su co the khoi chay — DOMAIN_RULES §10.3.
    // TODO(A21): tra cuu DetectedProcess de chinh xac hon.
    var canLaunch = !g.IsDlc && (
        !string.IsNullOrWhiteSpace(g.LaunchTargetPath) ||
        !string.IsNullOrWhiteSpace(g.ExeName));

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
        // Card va Queue dung GALLERY — khop WPF (MainWindow.xaml:348).
        GImagePath: g.GImagePath,
        ImagePath: g.ImagePath,
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

static GameDetailDto ToGameDetailDto(Game g, string? baseGameName)
{
    var list = ToDto(g);
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
        GImagePath: list.GImagePath,
        ImagePath: list.ImagePath,
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
        SteamLibraryImagePath: g.SteamLibraryImagePath,
        Genres: g.GameGenres.OrderBy(link => link.Genre.SortOrder)
            .ThenBy(link => link.Genre.Name)
            .Select(link => new TaxonomyItemDto(link.Genre.Id, link.Genre.Name, link.Genre.IconData, link.Genre.SortOrder))
            .ToList(),
        Themes: g.GameThemes.OrderBy(link => link.Theme.SortOrder)
            .ThenBy(link => link.Theme.Name)
            .Select(link => new TaxonomyItemDto(link.Theme.Id, link.Theme.Name, link.Theme.IconData, link.Theme.SortOrder))
            .ToList(),
        BaseGameId: g.BaseGameId,
        BaseGameName: baseGameName,
        Finishes: g.Finishes.OrderByDescending(f => f.FinishedAt)
            .Select(f => new GameFinishEntryDto(f.Id, f.FinishedAt, f.Score))
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
