using MasterPlay.Core.Entities;
using MasterPlay.Host.Notifications;
using MasterPlay.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MasterPlay.Host.Tests;

/// <summary>DOMAIN_RULES §22 — feed theo chu thu vien va trang thai da xem luu o SQLite.</summary>
public sealed class ReleaseNotificationEndpointsTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly MasterPlayContext _db;

    public ReleaseNotificationEndpointsTests()
    {
        _connection.Open();
        _db = new MasterPlayContext(new DbContextOptionsBuilder<MasterPlayContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        // Cung DDL voi DataService (bang tao bang SQL tho, khong nam trong model EF).
        _db.Database.ExecuteSqlRaw(
            """
            CREATE TABLE IF NOT EXISTS ReleaseNotificationReads (
                LibraryOwnerKey TEXT NOT NULL,
                NotificationId TEXT NOT NULL,
                ReadAt TEXT NOT NULL,
                CONSTRAINT PK_ReleaseNotificationReads PRIMARY KEY (LibraryOwnerKey, NotificationId)
            );
            """);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private void AddGame(string id, string owner, int daysFromTodayUtc, bool disabled = false)
    {
        _db.Games.Add(new Game
        {
            Id = id,
            Name = id,
            LibraryOwnerKey = owner,
            IsDisabled = disabled,
            PublishDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(daysFromTodayUtc),
        });
        _db.SaveChanges();
    }

    private async Task<ReleaseNotificationFeedDto> Feed(string owner)
    {
        var result = await ReleaseNotificationEndpoints.GetFeed(_db, owner, "UTC", CancellationToken.None);
        return Assert.IsType<Ok<ReleaseNotificationFeedDto>>(result).Value!;
    }

    [Fact]
    public async Task Feed_only_contains_active_games_of_the_owner_and_read_state_persists()
    {
        AddGame("mine", "user:a", 2);
        AddGame("mine-later", "user:a", 10);
        AddGame("mine-disabled", "user:a", 1, disabled: true);
        AddGame("theirs", "user:b", 2);

        var feed = await Feed("user:a");
        var item = Assert.Single(feed.Items);
        Assert.Equal("mine", item.GameId);
        Assert.Equal(1, feed.UnreadCount);

        var marked = await ReleaseNotificationEndpoints.MarkRead(
            _db, "user:a", new MarkReleaseNotificationsReadDto([item.Id, item.Id]), CancellationToken.None);
        Assert.IsType<NoContent>(marked);

        var after = await Feed("user:a");
        Assert.True(Assert.Single(after.Items).Read);
        Assert.Equal(0, after.UnreadCount);

        // Trang thai da xem khong lan sang chu thu vien khac.
        Assert.Equal(1, (await Feed("user:b")).UnreadCount);
    }

    [Fact]
    public async Task Invalid_requests_are_rejected()
    {
        var badZone = await ReleaseNotificationEndpoints.GetFeed(_db, "user:a", "Mars/Olympus_Mons", CancellationToken.None);
        Assert.Equal(400, Assert.IsAssignableFrom<IStatusCodeHttpResult>(badZone).StatusCode);

        var noIds = await ReleaseNotificationEndpoints.MarkRead(_db, "user:a", new MarkReleaseNotificationsReadDto([]), CancellationToken.None);
        Assert.Equal(400, Assert.IsAssignableFrom<IStatusCodeHttpResult>(noIds).StatusCode);

        var badId = await ReleaseNotificationEndpoints.MarkRead(
            _db, "user:a", new MarkReleaseNotificationsReadDto(["not-an-id"]), CancellationToken.None);
        Assert.Equal(400, Assert.IsAssignableFrom<IStatusCodeHttpResult>(badId).StatusCode);
    }
}
