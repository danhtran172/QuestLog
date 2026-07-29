using MasterPlay.Models;
using MasterPlay.Models.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MasterPlay.TrayApp.Services;

public sealed class PlayTimeService : IDisposable
{
    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan GameRefreshInterval = TimeSpan.FromMinutes(1);
    private readonly ILogger<PlayTimeService> _logger;
    private readonly ConcurrentDictionary<string, int> _pendingSeconds = new();
    private readonly HashSet<string> _runningGameIds = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _flushLock = new(1, 1);
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
        if (_worker is { IsCompleted: false })
            return;

        _cancellation = new CancellationTokenSource();
        _worker = RunAsync(_cancellation.Token);
        _logger.LogInformation("Playtime tracking started");
    }

    public void Stop()
    {
        if (_cancellation == null)
            return;

        _cancellation.Cancel();
        try
        {
            _worker?.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }

        FlushAsync(CancellationToken.None).GetAwaiter().GetResult();
        _cancellation.Dispose();
        _cancellation = null;
        _worker = null;
        _logger.LogInformation("Playtime tracking stopped");
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(SampleInterval);
        var refreshAt = DateTimeOffset.MinValue;
        var flushAt = DateTimeOffset.UtcNow + FlushInterval;

        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            var now = DateTimeOffset.UtcNow;
            if (now >= refreshAt)
            {
                _games = DataService.LoadActiveGames();
                refreshAt = now + GameRefreshInterval;
            }

            SampleGames(now);

            if (now >= flushAt)
            {
                await FlushAsync(cancellationToken);
                flushAt = now + FlushInterval;
            }
        }
    }

    private void SampleGames(DateTimeOffset now)
    {
        var foregroundWindow = GetForegroundWindow();
        GetWindowThreadProcessId(foregroundWindow, out var foregroundProcessId);
        var userIsActive = GetIdleTime() < TimeSpan.FromSeconds(30);
        var currentlyRunning = new HashSet<string>(StringComparer.Ordinal);

        foreach (var game in _games)
        {
            var processName = NormalizeProcessName(game.ExeName);
            if (processName.Length == 0)
                continue;

            Process[] processes;
            try
            {
                processes = Process.GetProcessesByName(processName);
            }
            catch
            {
                continue;
            }

            try
            {
                if (processes.Length == 0)
                    continue;

                currentlyRunning.Add(game.Id);
                if (userIsActive && processes.Any(x => x.Id == foregroundProcessId))
                    _pendingSeconds.AddOrUpdate(game.Id, (int)SampleInterval.TotalSeconds, (_, value) => value + (int)SampleInterval.TotalSeconds);
            }
            finally
            {
                foreach (var process in processes)
                    process.Dispose();
            }
        }

        foreach (var stoppedId in _runningGameIds.Except(currentlyRunning).ToArray())
        {
            var game = _games.FirstOrDefault(x => x.Id == stoppedId);
            if (game != null)
                GameStopped?.Invoke(this, new GameActivity(game, now));
        }

        _runningGameIds.Clear();
        _runningGameIds.UnionWith(currentlyRunning);
    }

    private async Task FlushAsync(CancellationToken cancellationToken)
    {
        if (_pendingSeconds.IsEmpty || !await _flushLock.WaitAsync(0, cancellationToken))
            return;

        try
        {
            var batch = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var entry in _pendingSeconds.ToArray())
            {
                if (_pendingSeconds.TryRemove(entry.Key, out var seconds))
                    batch[entry.Key] = seconds;
            }

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

    private static string NormalizeProcessName(string? executableName)
    {
        return Path.GetFileNameWithoutExtension(executableName?.Trim() ?? string.Empty);
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
