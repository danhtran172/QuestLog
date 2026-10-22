using MasterPlay.Infrastructure.Data.Services;
using MasterPlay.Infrastructure.Processes;
using MasterPlay.Core.Entities;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace MasterPlay.TrayApp.Services;

public sealed class PlayTimeService : IDisposable
{
    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan GameRefreshInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan IdleThreshold = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(10);

    private readonly ILogger<PlayTimeService> _logger;
    private readonly ConcurrentDictionary<string, int> _pendingSeconds = new();
    private readonly Dictionary<string, Game> _runningGames = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _flushLock = new(1, 1);
    private readonly object _lifecycleGate = new();
    private CancellationTokenSource? _cancellation;
    private Task? _worker;
    private List<Game> _games = [];

    public event EventHandler<GameActivity>? GameStopped;

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LastInputInfo lastInputInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint Size;
        public uint Time;
    }

    public PlayTimeService(ILogger<PlayTimeService> logger)
    {
        _logger = logger;
    }

    public void Start()
    {
        lock (_lifecycleGate)
        {
            if (_worker is { IsCompleted: false })
                return;

            var cancellation = new CancellationTokenSource();
            _cancellation = cancellation;

            // Task.Run la BAT BUOC. Start() duoc goi tu UI thread cua WinForms, noi
            // da co SynchronizationContext. Neu goi thang RunAsync thi moi doan sau
            // "await" bi day nguoc ve UI thread; Stop() dung tren UI thread cho worker
            // xong -> hai ben cho nhau -> tray dong bang vinh vien.
            _worker = Task.Run(() => RunAsync(cancellation.Token));
            _logger.LogInformation("Playtime tracking started");
        }
    }

    public void Stop()
    {
        CancellationTokenSource cancellation;
        Task? worker;

        lock (_lifecycleGate)
        {
            if (_cancellation == null)
                return;

            cancellation = _cancellation;
            worker = _worker;
            _cancellation = null;
            _worker = null;
        }

        cancellation.Cancel();

        // RunAsync khong nem ra ngoai nen Wait chi cho worker ket thuc. Van dat han
        // de mot lan treo bat ngo khong khoa luon menu tray.
        try
        {
            if (worker != null && !worker.Wait(StopTimeout))
                _logger.LogWarning("Playtime worker did not stop within {Timeout}", StopTimeout);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Playtime worker ended with an error");
        }

        // Lan flush cuoi cung cung phai roi khoi UI thread, vi no await xuong CSDL.
        try
        {
            if (!Task.Run(() => FlushAsync(CancellationToken.None)).Wait(StopTimeout))
                _logger.LogWarning("Final playtime flush did not finish within {Timeout}", StopTimeout);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Final playtime flush failed");
        }

        cancellation.Dispose();
        _logger.LogInformation("Playtime tracking stopped");
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(SampleInterval);
        var refreshAt = DateTimeOffset.MinValue;
        var flushAt = DateTimeOffset.UtcNow + FlushInterval;

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                var now = DateTimeOffset.UtcNow;

                if (now >= refreshAt)
                {
                    // Mot tick hong khong duoc giet ca vong lap. Truoc day CSDL ban
                    // khoa mot lan la worker chet am tham, menu van hien dang bat va
                    // khong con giay choi nao duoc ghi cho den khi khoi dong lai.
                    refreshAt = TryRefreshGames()
                        ? now + GameRefreshInterval
                        : now + SampleInterval;
                }

                await SampleGamesSafeAsync(now, cancellationToken);

                if (now >= flushAt)
                {
                    await FlushAsync(cancellationToken);
                    flushAt = now + FlushInterval;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Playtime worker stopped unexpectedly");
        }
    }

    private bool TryRefreshGames()
    {
        try
        {
            _games = DataService.LoadWatchedGames();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not refresh the active game list");
            return false;
        }
    }

    private async Task SampleGamesSafeAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        try
        {
            await SampleGamesAsync(now, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Playtime sampling failed for this tick");
        }
    }

    private async Task SampleGamesAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var foregroundWindow = GetForegroundWindow();
        GetWindowThreadProcessId(foregroundWindow, out var foregroundProcessId);
        var userIsActive = GetIdleTime() < IdleThreshold;
        var currentlyRunning = new Dictionary<string, Game>(StringComparer.Ordinal);
        // Cung anh chup va quy tac khop ExeName voi GameProcessMonitor cua Host (isRunning),
        // nen game hien "Playing" tren UI cung chinh la game dang duoc dem gio.
        var processes = GameProcessSnapshot.Capture();

        foreach (var game in _games)
        {
            var processIds = processes.ProcessIdsFor(game);
            if (processIds.Count == 0)
                continue;

            currentlyRunning[game.Id] = game;
            if (userIsActive && processIds.Contains((int)foregroundProcessId))
                _pendingSeconds.AddOrUpdate(game.Id, (int)SampleInterval.TotalSeconds, (_, value) => value + (int)SampleInterval.TotalSeconds);
        }

        // Giu ca doi tuong Game chu khong chi Id: game vua bi go khoi danh sach
        // active van phai bao duoc da tat cho nguoi dang ky GameStopped.
        foreach (var stopped in _runningGames.Where(entry => !currentlyRunning.ContainsKey(entry.Key)).ToArray())
            RaiseGameStopped(new GameActivity(stopped.Value, now));

        var startedGameIds = currentlyRunning.Keys
            .Where(id => !_runningGames.ContainsKey(id))
            .ToArray();

        if (startedGameIds.Length > 0)
        {
            try
            {
                await DataService.RecordLastPlayedAsync(startedGameIds, now.UtcDateTime, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to persist last-played timestamps");
            }
        }

        _runningGames.Clear();
        foreach (var entry in currentlyRunning)
            _runningGames[entry.Key] = entry.Value;
    }

    // Mot handler nem loi khong duoc keo theo ca vong lap dem
    // gio, va cung khong duoc chan cac handler con lai.
    private void RaiseGameStopped(GameActivity activity)
    {
        var handlers = GameStopped?.GetInvocationList()
            .Cast<EventHandler<GameActivity>>() ?? [];

        foreach (var handler in handlers)
        {
            try
            {
                handler(this, activity);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "A game-stopped handler failed for {GameName}", activity.Game.Name);
            }
        }
    }

    private async Task FlushAsync(CancellationToken cancellationToken)
    {
        if (_pendingSeconds.IsEmpty)
            return;

        // Khong truyen token vao WaitAsync: luc dung service token da bi huy, cho
        // token vao day thi lan flush cuoi nem OperationCanceledException va toan bo
        // so giay dang cho bi vut di.
        if (!await _flushLock.WaitAsync(0))
            return;

        try
        {
            var batch = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var entry in _pendingSeconds.ToArray())
            {
                if (_pendingSeconds.TryRemove(entry.Key, out var seconds))
                    batch[entry.Key] = seconds;
            }

            if (batch.Count == 0)
                return;

            try
            {
                await DataService.RecordPlaytimeAsync(batch, cancellationToken);
            }
            catch
            {
                foreach (var entry in batch)
                    _pendingSeconds.AddOrUpdate(entry.Key, entry.Value, (_, value) => value + entry.Value);
                throw;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to persist playtime batch");
        }
        finally
        {
            _flushLock.Release();
        }
    }

    private static TimeSpan GetIdleTime()
    {
        var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
        return GetLastInputInfo(ref info)
            ? TimeSpan.FromMilliseconds(unchecked((uint)Environment.TickCount - info.Time))
            : TimeSpan.Zero;
    }

    public void Dispose()
    {
        Stop();
        _flushLock.Dispose();
    }
}
