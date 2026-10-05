using MasterPlay.Host.Auth;
using MasterPlay.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MasterPlay.Host.Notifications;

/// <summary>
/// Thong bao game trong thu vien sap ra mat. DOMAIN_RULES §22, API_CONTRACT §9.1.
/// Trang thai "da xem" luu theo chu thu vien trong bang ReleaseNotificationReads
/// (tao o DataService), nen dong bo giua cac cua so cung tai khoan tren may nay.
/// </summary>
public static class ReleaseNotificationEndpoints
{
    // Hang da xem cu hon moc nay khong con trung thong bao nao nua.
    private static readonly TimeSpan ReadRetention = TimeSpan.FromDays(30);

    public static void MapReleaseNotificationEndpoints(this WebApplication app)
    {
        app.MapGet("/api/notifications/releases", (HttpContext http, MasterPlayContext db, string? timeZone, CancellationToken ct) =>
            WithOwner(http, ownerKey => GetFeed(db, ownerKey, timeZone, ct)));

        app.MapPost("/api/notifications/releases/read", (HttpContext http, MasterPlayContext db, MarkReleaseNotificationsReadDto? body, CancellationToken ct) =>
            WithOwner(http, ownerKey => MarkRead(db, ownerKey, body, ct)));
    }

    // Cung quy tac voi TryGetLibraryOwnerKey trong Program.cs (ham cuc bo top-level,
    // khong goi duoc tu day). Doi mot ben thi doi ca ben kia.
    private static Task<IResult> WithOwner(HttpContext http, Func<string, Task<IResult>> handler)
    {
        if (http.Items["QuestLogGuest"] is true)
            return handler("guest:local");
        if (http.Items["QuestLogAccount"] is AuthAccountDto account && !string.IsNullOrWhiteSpace(account.Id))
            return handler($"user:{account.Id}");
        return Task.FromResult(Error(401, "AUTH_REQUIRED", "Sign in or continue as guest to view notifications."));
    }

    public static async Task<IResult> GetFeed(MasterPlayContext db, string ownerKey, string? timeZone, CancellationToken ct)
    {
        if (!ReleaseReminders.TryResolveTimeZone(timeZone, out var zone))
            return Error(400, "INVALID_TIME_ZONE", "timeZone must be an IANA or Windows time zone id.");

        var now = DateTimeOffset.UtcNow;
        // Ngay dia phuong lech ngay UTC toi da mot ngay (UTC-12..UTC+14); lay rong
        // hai ngay moi phia roi de ReleaseReminders quyet dinh theo mui gio that.
        var todayUtc = DateOnly.FromDateTime(now.UtcDateTime);
        var from = todayUtc.AddDays(-2);
        var to = todayUtc.AddDays(ReleaseReminders.LeadDays + 2);

        var rows = await db.Games.AsNoTracking()
            .Where(game => game.LibraryOwnerKey == ownerKey && !game.IsDisabled && game.PublishDate != null)
            .Select(game => new { game.Id, game.Name, game.PublishDate, game.BannerPath, game.PosterPath })
            .ToListAsync(ct);

        var candidates = rows
            .Where(row => row.PublishDate!.Value >= from && row.PublishDate.Value <= to)
            .Select(row => new ReleaseCandidate(row.Id, row.Name, row.PublishDate!.Value, row.BannerPath, row.PosterPath))
            .ToList();

        var readIds = candidates.Count == 0
            ? new HashSet<string>(StringComparer.Ordinal)
            : (await db.Database
                .SqlQuery<string>($"SELECT NotificationId AS Value FROM ReleaseNotificationReads WHERE LibraryOwnerKey = {ownerKey}")
                .ToListAsync(ct))
                .ToHashSet(StringComparer.Ordinal);

        return Results.Ok(ReleaseReminders.Build(candidates, zone, now, readIds));
    }

    public static async Task<IResult> MarkRead(MasterPlayContext db, string ownerKey, MarkReleaseNotificationsReadDto? body, CancellationToken ct)
    {
        var ids = body?.Ids;
        if (ids is null || ids.Count == 0)
            return Error(400, "NOTIFICATION_IDS_REQUIRED", "ids must contain at least one notification id.");
        if (ids.Count > ReleaseReminders.MaxReadIdsPerRequest)
            return Error(400, "NOTIFICATION_IDS_TOO_MANY", $"ids may contain at most {ReleaseReminders.MaxReadIdsPerRequest} items.");
        if (ids.Any(id => !ReleaseReminders.IsValidNotificationId(id)))
            return Error(400, "NOTIFICATION_ID_INVALID", "Each id must be a release notification id returned by the API.");

        var readAt = DateTimeOffset.UtcNow.ToString("O");
        var expiry = DateTimeOffset.UtcNow.Subtract(ReadRetention).ToString("O");

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        foreach (var id in ids.Distinct(StringComparer.Ordinal))
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT OR IGNORE INTO ReleaseNotificationReads (LibraryOwnerKey, NotificationId, ReadAt) VALUES ({ownerKey}, {id}, {readAt})",
                ct);
        }
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM ReleaseNotificationReads WHERE LibraryOwnerKey = {ownerKey} AND ReadAt < {expiry}",
            ct);
        await transaction.CommitAsync(ct);

        return Results.NoContent();
    }

    private static IResult Error(int status, string code, string message) =>
        Results.Json(new { error = new { code, message } }, statusCode: status);
}
