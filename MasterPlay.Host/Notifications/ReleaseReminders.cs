using System.Text.RegularExpressions;

namespace MasterPlay.Host.Notifications;

/// <summary>Mot game trong thu vien du dieu kien xet nhac ra mat.</summary>
public sealed record ReleaseCandidate(
    string GameId,
    string Name,
    DateOnly PublishDate,
    string? BannerPath,
    string? PosterPath);

public sealed record ReleaseNotificationDto(
    string Id,
    string GameId,
    string GameName,
    string? BannerPath,
    string? PosterPath,
    string Stage,
    int DaysLeft,
    string PublishDate,
    DateTimeOffset ReleaseAt,
    bool Read);

public sealed record ReleaseNotificationFeedDto(
    string TimeZone,
    DateTimeOffset GeneratedAt,
    int UnreadCount,
    IReadOnlyList<ReleaseNotificationDto> Items);

public sealed record MarkReleaseNotificationsReadDto(IReadOnlyList<string>? Ids);

/// <summary>
/// Nhac game sap ra mat. DOMAIN_RULES §22.
///
/// `PublishDate` la NGAY theo UTC (IGDB `first_release_date` la Unix seconds UTC,
/// xem IgdbService.ToDateOnly). Moc ra mat = 00:00 UTC cua ngay do, doi sang mui
/// gio cua nguoi dung roi moi so ngay — nguoi o UTC+7 thay game ra mat luc 07:00
/// cung ngay, nguoi o UTC-5 thay 19:00 ngay hom truoc.
///
/// Hai moc nhac, moi moc la mot thong bao rieng (doc rieng):
///   - "soon":  con 1..3 ngay theo lich dia phuong;
///   - "today": dung ngay ra mat theo lich dia phuong.
/// Id gom ca ngay phat hanh, nen doi ngay phat hanh thi thong bao moi hien lai.
/// </summary>
public static partial class ReleaseReminders
{
    public const int LeadDays = 3;
    public const string StageSoon = "soon";
    public const string StageToday = "today";
    public const int MaxReadIdsPerRequest = 200;

    public static DateTimeOffset ReleaseInstant(DateOnly publishDate) =>
        new(publishDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    public static string NotificationId(string gameId, string stage, DateOnly publishDate) =>
        $"{gameId}:{stage}:{publishDate:yyyy-MM-dd}";

    public static bool IsValidNotificationId(string? id) =>
        !string.IsNullOrWhiteSpace(id) && id.Length <= 200 && NotificationIdPattern().IsMatch(id);

    /// <summary>
    /// Rong/null = mui gio cua may chay Host (Local API chay tren may nguoi dung).
    /// Nhan ca ten IANA ("Asia/Ho_Chi_Minh") lan ten Windows.
    /// </summary>
    public static bool TryResolveTimeZone(string? id, out TimeZoneInfo zone)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            zone = TimeZoneInfo.Local;
            return true;
        }

        var trimmed = id.Trim();
        if (trimmed.Length <= 64 && TimeZoneInfo.TryFindSystemTimeZoneById(trimmed, out var found))
        {
            zone = found;
            return true;
        }

        zone = TimeZoneInfo.Utc;
        return false;
    }

    public static ReleaseNotificationFeedDto Build(
        IEnumerable<ReleaseCandidate> games,
        TimeZoneInfo zone,
        DateTimeOffset now,
        IReadOnlySet<string> readIds)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        var items = new List<ReleaseNotificationDto>();

        foreach (var game in games)
        {
            var releaseAt = ReleaseInstant(game.PublishDate);
            var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(releaseAt, zone).DateTime);
            var daysLeft = localDate.DayNumber - today.DayNumber;
            var stage = daysLeft switch
            {
                0 => StageToday,
                >= 1 and <= LeadDays => StageSoon,
                _ => null,
            };
            if (stage is null) continue;

            var id = NotificationId(game.GameId, stage, game.PublishDate);
            items.Add(new ReleaseNotificationDto(
                id,
                game.GameId,
                game.Name,
                game.BannerPath,
                game.PosterPath,
                stage,
                daysLeft,
                game.PublishDate.ToString("yyyy-MM-dd"),
                releaseAt,
                readIds.Contains(id)));
        }

        var ordered = items
            .OrderBy(item => item.DaysLeft)
            .ThenBy(item => item.GameName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.GameId, StringComparer.Ordinal)
            .ToList();

        return new ReleaseNotificationFeedDto(
            zone.Id,
            now,
            ordered.Count(item => !item.Read),
            ordered);
    }

    [GeneratedRegex(@"^.+:(soon|today):\d{4}-\d{2}-\d{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex NotificationIdPattern();
}
