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
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(30);

    private readonly ILogger<BackupService> _logger;
    private readonly PlayTimeService _playTimeService;
    private readonly ConcurrentDictionary<string, Game> _queue = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, long> _debounceGenerations = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastBackups = new(StringComparer.Ordinal);
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly object _watcherGate = new();
    private readonly object _lifecycleGate = new();
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
        lock (_lifecycleGate)
        {
            if (_worker is { IsCompleted: false })
                return;

            var cancellation = new CancellationTokenSource();
            _cancellation = cancellation;
            _playTimeService.GameStopped += OnGameStopped;
            ConfigureWatchers();

            // Xem ghi chu trong PlayTimeService.Start: worker phai bat dau tren
            // thread pool, neu khong Stop() tu menu tray se treo.
            _worker = Task.Run(() => RunAsync(cancellation.Token));
            _logger.LogInformation("Backup monitoring started");
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

        _playTimeService.GameStopped -= OnGameStopped;
        cancellation.Cancel();
        Signal();
        DisposeWatchers();

        try
        {
            if (worker != null && !worker.Wait(StopTimeout))
                _logger.LogWarning("Backup worker did not stop within {Timeout}", StopTimeout);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup worker ended with an error");
        }

        _debounceGenerations.Clear();
        cancellation.Dispose();
        _logger.LogInformation("Backup monitoring stopped");
    }

    /// <summary>
    /// Xep hang sao luu cho moi game dang active co thu muc save. Dung cho muc
    /// "Backup ngay" o menu tray. Tra ve so game da xep hang.
    /// Doc CSDL chay tren thread pool vi menu goi tu UI thread.
    /// </summary>
    public async Task<int> RequestBackupAllAsync()
    {
        if (_cancellation == null)
            return 0;

        List<Game> games;
        try
        {
            games = await Task.Run(DataService.LoadActiveGames);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not load active games for a manual backup");
            return 0;
        }

        var queued = 0;
        foreach (var game in games)
        {
            if (game.IsDlc)
                continue;

            if (string.IsNullOrWhiteSpace(game.SaveFolderPath) || !Directory.Exists(game.SaveFolderPath))
                continue;

            QueueBackup(game);
            queued++;
        }

        if (queued > 0)
            _logger.LogInformation("Manual backup queued for {Count} games", queued);

        return queued;
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

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await _signal.WaitAsync(cancellationToken);
                foreach (var entry in _queue.ToArray())
                {
                    if (_queue.TryRemove(entry.Key, out var game))
                        await BackupGameAsync(game, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup worker stopped unexpectedly");
        }
        finally
        {
            // Phai await trong finally. Ban cu thoat vong lap bang ngoai le nen dong
            // "await refreshTask" khong bao gio chay, loi cua no thanh unobserved
            // task exception va PeriodicTimer bi dispose ngay duoi chan no.
            await refreshTask;
        }
    }

    private async Task RefreshWatchersAsync(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
                ConfigureWatchers();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup watcher refresh stopped unexpectedly");
        }
    }

    private void ConfigureWatchers()
    {
        List<Game> games;
        try
        {
            games = DataService.LoadActiveGames();
        }
        catch (Exception ex)
        {
            // CSDL ban khoa khong duoc lam chet Start() hay vong lam moi watcher.
            _logger.LogError(ex, "Could not load active games for backup monitoring");
            return;
        }

        // Vong lam moi chay tren thread pool moi 5 phut, Stop() chay tren UI thread:
        // hai ben dung chung danh sach _watchers nen phai khoa.
        lock (_watcherGate)
        {
            DisposeWatchersLocked();

            foreach (var game in games)
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
                    watcher.Error += (_, e) => OnWatcherError(game, e);
                    watcher.EnableRaisingEvents = true;
                    _watchers.Add(watcher);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Unable to monitor save folder for {GameName}", game.Name);
                }
            }
        }
    }

    // Watcher tu tat khi bo dem tran hoac thu muc save bien mat. Khong bat thi save
    // cua game do khong con duoc sao luu ma khong co dau vet nao.
    private void OnWatcherError(Game game, System.IO.ErrorEventArgs e)
    {
        _logger.LogWarning(
            e.GetException(),
            "The save-folder watcher for {GameName} reported an error",
            game.Name);
    }

    private void DisposeWatchers()
    {
        lock (_watcherGate)
            DisposeWatchersLocked();
    }

    private void DisposeWatchersLocked()
    {
        foreach (var watcher in _watchers)
        {
            try
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not dispose a save-folder watcher");
            }
        }

        _watchers.Clear();
    }

    private void DebounceBackup(Game game)
    {
        var cancellation = _cancellation;
        if (cancellation == null)
            return;

        // So sanh "the he" thay cho mot CancellationTokenSource moi game. Ban cu huy
        // va dispose CTS cu ngay trong update factory cua AddOrUpdate: factory co the
        // chay lai khi tranh chap nen CTS bi dispose hai lan, va thread khac dang cam
        // chinh CTS do se nem ObjectDisposedException tren thread cua watcher, khong
        // ai bat, sap tien trinh tray.
        var generation = _debounceGenerations.AddOrUpdate(game.Id, 1, (_, value) => value + 1);

        _ = Task.Run(async () =>
        {
            try
            {
                var token = cancellation.Token;
                await Task.Delay(ChangeDebounce, token);

                if (_debounceGenerations.TryGetValue(game.Id, out var current) && current != generation)
                    return;

                QueueBackup(game);
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
                // Service dung giua chung, token khong con dung duoc nua.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Debounced backup for {GameName} failed", game.Name);
            }
        });
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
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task BackupGameAsync(Game game, CancellationToken cancellationToken)
    {
        if (game.IsDlc)
            return;

        if (_lastBackups.TryGetValue(game.Id, out var lastBackup))
        {
            var elapsed = DateTimeOffset.UtcNow - lastBackup;
            if (elapsed < MinimumBackupInterval)
            {
                // Khong duoc VUT yeu cau nay. Game thoat trong vong 2 phut sau lan
                // sao luu truoc thi save cuoi cua ca phien choi mat hut. Hen lai dung
                // luc het cua so cho, hang doi da khu trung theo Id nen khong don.
                ScheduleRetry(game, MinimumBackupInterval - elapsed);
                return;
            }
        }

        try
        {
            await _backupLock.WaitAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
        {
            return;
        }

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

            try
            {
                await Task.Run(() => CopyDirectory(game.SaveFolderPath, destination, cancellationToken), cancellationToken);
            }
            catch
            {
                // Mot thu muc sao luu do dang con nguy hiem hon khong co ban sao nao:
                // nguoi dung se tuong day la mot ban save day du va phuc hoi tu no.
                TryDeleteDirectory(destination);
                throw;
            }

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

    private void ScheduleRetry(Game game, TimeSpan delay)
    {
        var cancellation = _cancellation;
        if (cancellation == null)
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                var token = cancellation.Token;
                await Task.Delay(delay, token);
                QueueBackup(game);
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not re-queue the backup for {GameName}", game.Name);
            }
        });
    }

    private void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not remove the incomplete backup folder {Path}", path);
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
