using MasterPlay.Host.Notifications;

namespace MasterPlay.Host.Tests;

/// <summary>DOMAIN_RULES §22 — nhac game sap ra mat theo mui gio nguoi dung.</summary>
public sealed class ReleaseRemindersTests
{
    private static readonly IReadOnlySet<string> NoneRead = new HashSet<string>();

    private static TimeZoneInfo Zone(string id)
    {
        Assert.True(ReleaseReminders.TryResolveTimeZone(id, out var zone));
        return zone;
    }

    private static ReleaseCandidate Game(string id, string date) =>
        new(id, id, DateOnly.Parse(date), null, null);

    private static DateTimeOffset Utc(string value) => DateTimeOffset.Parse(value + "Z").ToUniversalTime();

    [Fact]
    public void Three_days_before_release_is_soon_and_release_day_is_today()
    {
        var zone = Zone("UTC");
        var games = new[] { Game("a", "2026-10-07"), Game("b", "2026-10-04"), Game("c", "2026-10-08"), Game("d", "2026-10-03") };

        var feed = ReleaseReminders.Build(games, zone, Utc("2026-10-04T10:00:00"), NoneRead);

        Assert.Equal(["b", "a"], feed.Items.Select(i => i.GameId));
        Assert.Equal(ReleaseReminders.StageToday, feed.Items[0].Stage);
        Assert.Equal(0, feed.Items[0].DaysLeft);
        Assert.Equal(ReleaseReminders.StageSoon, feed.Items[1].Stage);
        Assert.Equal(3, feed.Items[1].DaysLeft);
        Assert.Equal(2, feed.UnreadCount);
    }

    [Fact]
    public void Release_instant_is_midnight_utc_shifted_into_the_user_zone()
    {
        var game = new[] { Game("g", "2026-10-07") };
        // 2026-10-06 18:00 UTC = 2026-10-07 01:00 o Viet Nam (UTC+7): da toi ngay ra mat.
        var now = Utc("2026-10-06T18:00:00");

        var vietnam = ReleaseReminders.Build(game, Zone("Asia/Ho_Chi_Minh"), now, NoneRead);
        Assert.Equal(ReleaseReminders.StageToday, Assert.Single(vietnam.Items).Stage);
        Assert.Equal(Utc("2026-10-07T00:00:00"), vietnam.Items[0].ReleaseAt);

        // New York (UTC-4 mua he): 00:00 UTC ngay 7 = 20:00 ngay 6 — cung la "hom nay"
        // vi luc nay o New York van la 14:00 ngay 6.
        var newYork = ReleaseReminders.Build(game, Zone("America/New_York"), now, NoneRead);
        Assert.Equal(ReleaseReminders.StageToday, Assert.Single(newYork.Items).Stage);

        // Cung luc do o UTC van con 1 ngay.
        var utc = ReleaseReminders.Build(game, Zone("UTC"), now, NoneRead);
        Assert.Equal(1, Assert.Single(utc.Items).DaysLeft);
    }

    [Fact]
    public void Each_stage_is_a_separate_notification_and_read_state_is_kept_per_id()
    {
        var game = new[] { Game("g", "2026-10-07") };
        var soonId = ReleaseReminders.NotificationId("g", ReleaseReminders.StageSoon, DateOnly.Parse("2026-10-07"));
        var read = new HashSet<string> { soonId };

        var soon = ReleaseReminders.Build(game, Zone("UTC"), Utc("2026-10-05T00:00:00"), read);
        Assert.True(Assert.Single(soon.Items).Read);
        Assert.Equal(0, soon.UnreadCount);

        var today = ReleaseReminders.Build(game, Zone("UTC"), Utc("2026-10-07T00:00:00"), read);
        Assert.False(Assert.Single(today.Items).Read);
        Assert.Equal(1, today.UnreadCount);
    }

    [Fact]
    public void Time_zone_accepts_iana_and_windows_ids_and_rejects_unknown()
    {
        Assert.True(ReleaseReminders.TryResolveTimeZone("Asia/Ho_Chi_Minh", out _));
        Assert.True(ReleaseReminders.TryResolveTimeZone("SE Asia Standard Time", out _));
        Assert.True(ReleaseReminders.TryResolveTimeZone(null, out var local));
        Assert.Equal(TimeZoneInfo.Local.Id, local.Id);
        Assert.False(ReleaseReminders.TryResolveTimeZone("Mars/Olympus_Mons", out _));
    }

    [Theory]
    [InlineData("0b6f:soon:2026-10-07", true)]
    [InlineData("game:with:colons:today:2026-10-07", true)]
    [InlineData("g:later:2026-10-07", false)]
    [InlineData("g:soon:07/10/2026", false)]
    [InlineData("", false)]
    public void Notification_ids_are_validated(string id, bool valid) =>
        Assert.Equal(valid, ReleaseReminders.IsValidNotificationId(id));
}
