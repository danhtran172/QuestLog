using MasterPlay.Infrastructure.Data;
using MasterPlay.Infrastructure.Processes;
using Microsoft.EntityFrameworkCore;

namespace MasterPlay.Host;

/// <summary>
/// Game duoc do tien trinh (xem GameProcessWatch), kem chu thu vien de SSE chi bao game
/// cua minh. Launch target .exe cung la mot ten tien trinh cua game.
/// </summary>
public sealed record WatchedGame(
    string Id,
    string? ExeName,
    string OwnerKey,
    string? LaunchTargetKind = null,
    string? LaunchTargetPath = null);

/// <summary>
/// Nguon DUY NHAT tra loi "game nao dang chay" (DOMAIN_RULES §11, field `isRunning`).
/// ToDto doc tu day nen danh sach/chi tiet tai lan dau da dung; /api/events dang ky
/// <see cref="Subscribe"/> de day thay doi qua SSE. Chi giu id dang chay — khong dem gio
/// choi, viec do van la cua PlayTimeService ben Tray.
/// </summary>
public sealed class RunningGamesRegistry
{
    public static RunningGamesRegistry Shared { get; } = new();

    private readonly object _gate = new();
    private readonly List<Action> _subscribers = [];
    private IReadOnlyDictionary<string, string> _running = new Dictionary<string, string>(StringComparer.Ordinal);

    public bool IsRunning(string gameId) => Volatile.Read(ref _running).ContainsKey(gameId);

    /// <summary>Id game dang chay cua mot chu thu vien, xep on dinh de so sanh duoc.</summary>
    public IReadOnlyList<string> ForOwner(string ownerKey) => Volatile.Read(ref _running)
        .Where(entry => entry.Value == ownerKey)
        .Select(entry => entry.Key)
        .Order(StringComparer.Ordinal)
        .ToList();

    /// <summary>Thay snapshot. Chi bao nguoi dang ky khi tap game dang chay that su doi.</summary>
    public bool Publish(IEnumerable<WatchedGame> running)
    {
        var next = running
            .DistinctBy(game => game.Id)
            .ToDictionary(game => game.Id, game => game.OwnerKey, StringComparer.Ordinal);

        Action[] subscribers;
        lock (_gate)
        {
            var current = _running;
            if (current.Count == next.Count &&
                next.All(entry => current.TryGetValue(entry.Key, out var owner) && owner == entry.Value))
                return false;

            Volatile.Write(ref _running, next);
            subscribers = [.. _subscribers];
        }

        foreach (var notify in subscribers)
            notify();
        return true;
    }

    /// <summary>
    /// Nhan tin hieu moi khi snapshot doi. Callback chay tren luong cua monitor nen phai
    /// nhe (vd. danh thuc mot SemaphoreSlim); dispose de huy dang ky.
    /// </summary>
    public IDisposable Subscribe(Action changed)
    {
        lock (_gate)
            _subscribers.Add(changed);
        return new Subscription(this, changed);
    }

    private sealed class Subscription(RunningGamesRegistry owner, Action changed) : IDisposable
    {
        public void Dispose()
        {
            lock (owner._gate)
                owner._subscribers.Remove(changed);
        }
    }
}

/// <summary>
/// Do tien trinh moi 2 giay (DOMAIN_RULES §11) va day ket qua vao
/// <see cref="RunningGamesRegistry"/>. Khop ExeName bang <see cref="GameProcessSnapshot"/>,
/// chung voi PlayTimeService ben Tray, nen game mo qua .lnk hay launcher (Steam...) van
/// duoc nhan khi tien trinh game that hien len. Tu do chu khong hoi Tray: Tray tat hoac
/// tat playtime tracking thi nut Play van phai biet game dang chay de khong mo hai lan.
/// Chi chay tren Windows — cung dieu kien voi capability `launch`.
/// </summary>
public sealed class GameProcessMonitor(
    RunningGamesRegistry registry,
    ILogger<GameProcessMonitor> logger) : BackgroundService
{
    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(2);
    // Sua ExeName trong editor thi toi da chung nay giay sau monitor moi biet.
    private static readonly TimeSpan GameRefreshInterval = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!OperatingSystem.IsWindows())
            return;

        using var timer = new PeriodicTimer(SampleInterval);
        IReadOnlyList<WatchedGame> games = [];
        var refreshAt = DateTimeOffset.MinValue;

        do
        {
            // Mot tick hong (CSDL dang khoa...) khong duoc giet ca vong lap: lan sau thu lai.
            try
            {
                var now = DateTimeOffset.UtcNow;
                if (now >= refreshAt)
                {
                    games = await LoadWatchedGamesAsync(stoppingToken);
                    refreshAt = now + GameRefreshInterval;
                }

                registry.Publish(MatchRunning(games, GameProcessSnapshot.Capture()));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Game process sampling failed for this tick");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    public static IEnumerable<WatchedGame> MatchRunning(IEnumerable<WatchedGame> games, GameProcessSnapshot processes) =>
        games.Where(game => processes.ProcessIdsFor(game.ExeName, game.LaunchTargetKind, game.LaunchTargetPath).Count > 0);

    private static async Task<IReadOnlyList<WatchedGame>> LoadWatchedGamesAsync(CancellationToken cancellationToken)
    {
        await using var db = new MasterPlayContext();
        return await db.Games.AsNoTracking()
            .Where(GameProcessWatch.Watchable)
            .Select(g => new WatchedGame(g.Id, g.ExeName, g.LibraryOwnerKey, g.LaunchTargetKind, g.LaunchTargetPath))
            .ToListAsync(cancellationToken);
    }
}
