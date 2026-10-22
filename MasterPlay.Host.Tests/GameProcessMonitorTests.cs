using MasterPlay.Infrastructure.Processes;

namespace MasterPlay.Host.Tests;

/// <summary>isRunning: game nao dang chay, va /api/events chi bao khi tap do doi.</summary>
public sealed class GameProcessMonitorTests
{
    private static WatchedGame Game(string id, string exeName, string owner = "user:a") => new(id, exeName, owner);

    private static GameProcessSnapshot Processes(params string[] names) =>
        new(names.Select((name, index) => (name, index + 100)));

    [Fact]
    public void MatchesProcessNameIgnoringCaseAndExeSuffix()
    {
        var games = new[] { Game("dredge", "DREDGE.exe"), Game("hades", "Hades"), Game("celeste", "Celeste") };

        var running = GameProcessMonitor.MatchRunning(games, Processes("dredge", "explorer", "HADES")).Select(g => g.Id);

        Assert.Equal(["dredge", "hades"], running);
    }

    [Fact]
    public void DottedExeNameKeepsEverythingBeforeExe()
    {
        // Tien trinh "Game.v2" khong duoc bi cat thanh "Game" roi khop nham game khac.
        var games = new[] { Game("v2", "Game.v2.exe"), Game("base", "Game.exe") };

        var running = GameProcessMonitor.MatchRunning(games, Processes("Game.v2")).Select(g => g.Id);

        Assert.Equal(["v2"], running);
    }

    [Fact]
    public void LaunchTargetExeCountsAsTheGameProcess()
    {
        // "Play God of War III.exe" chay game ngay trong tien trinh cua no, ten khac ExeName.
        var games = new[]
        {
            new WatchedGame("gow", "God of War III", "user:a", "exe", @"D:\Games\GoW\Play God of War III.exe"),
            new WatchedGame("lnk", "Other", "user:a", "shortcut", @"D:\Games\X\Play God of War III.lnk"),
            new WatchedGame("no-exe-name", null, "user:a", "exe", @"D:\Games\Y\Launcher.exe"),
        };

        var running = GameProcessMonitor.MatchRunning(games, Processes("Play God of War III", "launcher")).Select(g => g.Id);

        Assert.Equal(["gow", "no-exe-name"], running);
    }

    [Fact]
    public void GameProcessIdsJoinExeNameAndLaunchTarget()
    {
        // PlayTimeService so PID voi cua so dang focus: ca launcher lan game deu phai tinh.
        var snapshot = new GameProcessSnapshot([("Play God of War III", 1), ("God of War III", 2), ("explorer", 3)]);

        var ids = snapshot.ProcessIdsFor("God of War III", "exe", @"D:\Games\GoW\Play God of War III.exe");

        Assert.Equal([1, 2], ids.Order());
    }

    [Fact]
    public void SnapshotReturnsEveryProcessIdOfAGame()
    {
        // PlayTimeService can moi PID de biet cua so dang focus co phai cua game khong.
        var snapshot = new GameProcessSnapshot([("Hades", 10), ("hades", 11), ("explorer", 12)]);

        Assert.Equal([10, 11], snapshot.ProcessIdsFor(@"D:\Games\Hades\Hades.exe").Order());
        Assert.Empty(snapshot.ProcessIdsFor("Celeste"));
        Assert.False(snapshot.IsRunning("  "));
    }

    [Fact]
    public void PublishNotifiesOnlyWhenTheRunningSetChanges()
    {
        var registry = new RunningGamesRegistry();
        var notifications = 0;
        using var _ = registry.Subscribe(() => notifications++);

        Assert.True(registry.Publish([Game("dredge", "DREDGE")]));
        Assert.False(registry.Publish([Game("dredge", "DREDGE")]));
        Assert.True(registry.Publish([]));

        Assert.Equal(2, notifications);
        Assert.False(registry.IsRunning("dredge"));
    }

    [Fact]
    public void ForOwnerHidesGamesOfOtherLibraries()
    {
        var registry = new RunningGamesRegistry();
        registry.Publish([Game("b", "B", "user:a"), Game("a", "A", "user:a"), Game("x", "X", "guest:local")]);

        Assert.Equal(["a", "b"], registry.ForOwner("user:a"));
        Assert.Equal(["x"], registry.ForOwner("guest:local"));
        Assert.True(registry.IsRunning("x"));
    }

    [Fact]
    public void DisposedSubscriptionIsNoLongerNotified()
    {
        var registry = new RunningGamesRegistry();
        var notifications = 0;
        var subscription = registry.Subscribe(() => notifications++);
        subscription.Dispose();

        registry.Publish([Game("dredge", "DREDGE")]);

        Assert.Equal(0, notifications);
    }
}
