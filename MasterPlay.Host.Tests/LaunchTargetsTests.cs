using MasterPlay.Core.Entities;

namespace MasterPlay.Host.Tests;

/// <summary>canLaunch / tab console: nut Play chi hien khi file mo game con tren dia.</summary>
public sealed class LaunchTargetsTests
{
    private static LaunchTargets Targets(string[] existingFiles, params (string ExeName, string FullPath)[] detected)
    {
        var existing = new HashSet<string>(existingFiles, StringComparer.OrdinalIgnoreCase);
        return new LaunchTargets(detected, existing.Contains);
    }

    [Fact]
    public void ExeNameWithoutDetectedFile_IsNotLaunchable()
    {
        // Game da xoa khoi o: ExeName van con (de theo doi gio choi) nhung scan khong con thay exe.
        var targets = Targets([]);
        Assert.False(targets.CanLaunch(new Game { Id = "a", Name = "A", ExeName = "AC4BFSP" }));
    }

    [Fact]
    public void DetectedProcessWhoseFileWasDeleted_IsNotLaunchable()
    {
        var targets = Targets([], ("DREDGE.exe", @"D:\Games\DREDGE\DREDGE.exe"));
        Assert.False(targets.CanLaunch(new Game { Id = "a", Name = "A", ExeName = "DREDGE" }));
    }

    [Fact]
    public void ExeNameMatchesExistingDetectedFile_IsLaunchable()
    {
        const string path = @"D:\Games\DREDGE\DREDGE.exe";
        var targets = Targets([path], ("DREDGE.exe", path));
        var game = new Game { Id = "a", Name = "A", ExeName = "dredge" };

        Assert.True(targets.CanLaunch(game));
        Assert.Equal(path, targets.ResolveExistingPath(game));
    }

    [Fact]
    public void PicksTheDetectedCopyThatStillExists()
    {
        const string kept = @"D:\Games\B\Game.exe";
        var targets = Targets([kept], ("Game.exe", @"D:\Games\A\Game.exe"), ("Game.exe", kept));
        Assert.Equal(kept, targets.ResolveExistingPath(new Game { Id = "a", Name = "A", ExeName = "Game.exe" }));
    }

    [Fact]
    public void MissingLaunchTarget_IsNotLaunchable_EvenIfExeNameIsDetected()
    {
        const string detected = @"D:\Games\A\Game.exe";
        var targets = Targets([detected], ("Game.exe", detected));
        var game = new Game { Id = "a", Name = "A", ExeName = "Game", LaunchTargetPath = @"D:\Old\Game.lnk" };
        Assert.False(targets.CanLaunch(game));
    }

    [Fact]
    public void Dlc_IsNeverLaunchable()
    {
        const string path = @"D:\Games\A\Game.exe";
        var targets = Targets([path]);
        Assert.False(targets.CanLaunch(new Game { Id = "a", Name = "A", IsDlc = true, LaunchTargetPath = path }));
    }

    [Fact]
    public void ConsoleTab_KeepsOnlyGamesWithExistingFiles()
    {
        const string path = @"D:\Games\A\A.exe";
        var targets = Targets([path], ("A.exe", path));
        var games = new[]
        {
            new Game { Id = "installed", Name = "installed", ExeName = "A" },
            new Game { Id = "deleted", Name = "deleted", ExeName = "Gone" },
        };

        Assert.Equal(["installed"], LibraryQuery.ApplyTab(games, "console", targets).Select(g => g.Id));
    }
}
