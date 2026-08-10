using MasterPlay.Infrastructure.Data.Services;
using MasterPlay.Core.Entities;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace MasterPlay.TrayApp.Services;

public sealed class BackupService : IDisposable
{
    private static readonly TimeSpan WatcherRefreshInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ChangeDebounce = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan MinimumBackupInterval = TimeSpan.FromMinutes(2);
    private readonly ILogger<BackupService> _logger;
    private readonly PlayTimeService _playTimeService;
    private readonly ConcurrentDictionary<string, Game> _queue = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _debounces = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _lastBackups = new(StringComparer.Ordinal);
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly SemaphoreSlim _signal = new(0, 1);
    private readonly SemaphoreSlim _backupLock = new(1, 1);
    private CancellationTokenSource? _cancellation;
    private Task? _worker;

    public BackupService(ILogger<BackupService> logger, PlayTimeService playTimeService)
    {
        _logger = logger;
        _playTimeService = playTimeService;
    }

    public void Start()
    {
        if (_worker is { IsCompleted: false })
            return;

        _cancellation = new CancellationTokenSource();
        _playTimeService.GameStopped += OnGameStopped;
        ConfigureWatchers();
        _worker = RunAsync(_cancellation.Token);
        _logger.LogInformation("Backup monitoring started");
    }

    public void Stop()
    {
        if (_cancellation == null)
            return;

        _playTimeService.GameStopped -= OnGameStopped;
        _cancellation.Cancel();
        Signal();
        try
        {
            _worker?.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }

        _cancellation.Dispose();
        foreach (var watcher in _watchers)
            watcher.Dispose();
        _watchers.Clear();
        foreach (var debounce in _debounces.Values)
        {
            debounce.Cancel();
            debounce.Dispose();
        }
        _debounces.Clear();
        _cancellation = null;
        _worker = null;
        _logger.LogInformation("Backup monitoring stopped");
    }

    private void OnGameStopped(object? sender, GameActivity activity)
    {
        QueueBackup(activity.Game);
    }

    private void QueueBackup(Game game)
    {
        if (game.IsDlc)
            return;

        if (string.IsNullOrWhiteSpace(game.SaveFolderPath) || !Directory.Exists(game.SaveFolderPath))
            return;

        _queue[game.Id] = game;
        Signal();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var refreshTimer = new PeriodicTimer(WatcherRefreshInterval);
        var refreshTask = RefreshWatchersAsync(refreshTimer, cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            await _signal.WaitAsync(cancellationToken);
            foreach (var entry in _queue.ToArray())
            {
                if (_queue.TryRemove(entry.Key, out var game))
                    await BackupGameAsync(game, cancellationToken);
            }
        }

        await refreshTask;
    }

    private async Task RefreshWatchersAsync(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        while (await timer.WaitForNextTickAsync(cancellationToken))
            ConfigureWatchers();
    }

    private void ConfigureWatchers()
    {
        foreach (var watcher in _watchers)
            watcher.Dispose();
        _watchers.Clear();

        foreach (var game in DataService.LoadActiveGames())
        {
            if (string.IsNullOrWhiteSpace(game.SaveFolderPath) || !Directory.Exists(game.SaveFolderPath))
                continue;

            try
            {
                var watcher = new FileSystemWatcher(game.SaveFolderPath)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName |
                                   NotifyFilters.DirectoryName |
                                   NotifyFilters.LastWrite |
                                   NotifyFilters.Size
                };
                watcher.Changed += (_, _) => DebounceBackup(game);
                watcher.Created += (_, _) => DebounceBackup(game);
                watcher.Deleted += (_, _) => DebounceBackup(game);
                watcher.Renamed += (_, _) => DebounceBackup(game);
                watcher.EnableRaisingEvents = true;
                _watchers.Add(watcher);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to monitor save folder for {GameName}", game.Name);
            }
        }
    }

    private void DebounceBackup(Game game)
    {
        var parentToken = _cancellation?.Token ?? CancellationToken.None;
        var replacement = CancellationTokenSource.CreateLinkedTokenSource(parentToken);
        var previous = _debounces.AddOrUpdate(game.Id, replacement, (_, existing) =>
        {
            existing.Cancel();
            existing.Dispose();
            return replacement;
        });
        if (!ReferenceEquals(previous, replacement))
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(ChangeDebounce, replacement.Token);
                QueueBackup(game);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                if (_debounces.TryGetValue(game.Id, out var current) &&
                    ReferenceEquals(current, replacement) &&
                    _debounces.TryRemove(game.Id, out var removed))
                    removed.Dispose();
            }
        }, replacement.Token);
    }

    private void Signal()
    {
        try
        {
            _signal.Release();
        }
        catch (SemaphoreFullException)
        {
        }
    }

    private async Task BackupGameAsync(Game game, CancellationToken cancellationToken)
    {
        if (game.IsDlc)
            return;

        if (_lastBackups.TryGetValue(game.Id, out var lastBackup) &&
            DateTimeOffset.UtcNow - lastBackup < MinimumBackupInterval)
            return;

        await _backupLock.WaitAsync(cancellationToken);
        try
        {
            if (string.IsNullOrWhiteSpace(game.SaveFolderPath) || !Directory.Exists(game.SaveFolderPath))
                return;

            if (string.IsNullOrWhiteSpace(game.BackupFolderPath) || !Directory.Exists(game.BackupFolderPath))
            {
                if (!DataService.CreateBackupFolder(game))
                    return;
            }

            var destination = Path.Combine(
                game.BackupFolderPath!,
                $"Backup_{DateTime.Now:yyyyMMdd_HHmmss}");

            await Task.Run(() => CopyDirectory(game.SaveFolderPath, destination, cancellationToken), cancellationToken);
            _lastBackups[game.Id] = DateTimeOffset.UtcNow;
            _logger.LogInformation("Backup completed for {GameName}", game.Name);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Backup failed for {GameName}", game.Name);
        }
        finally
        {
            _backupLock.Release();
        }
    }

    private static void CopyDirectory(string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destinationPath);
        foreach (var directory in Directory.EnumerateDirectories(sourcePath, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.Combine(destinationPath, Path.GetRelativePath(sourcePath, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var destinationFile = Path.Combine(destinationPath, Path.GetRelativePath(sourcePath, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);
            File.Copy(file, destinationFile, true);
        }
    }

    public void Dispose()
    {
        Stop();
        _signal.Dispose();
        _backupLock.Dispose();
    }
}
