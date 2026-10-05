using MasterPlay.Infrastructure.Configuration;
using MasterPlay.Infrastructure.Data.Services;
using MasterPlay.Core.Entities;
using MasterPlay.Core.Metadata;
using Microsoft.Extensions.Logging;

namespace MasterPlay.TrayApp.Services;

public sealed class LibraryService : IDisposable
{
    private static readonly TimeSpan FullScanInterval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ChangeDebounce = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(15);
    private static readonly EnumerationOptions ScanOptions = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.System | FileAttributes.Hidden
    };

    private readonly ILogger<LibraryService> _logger;
    private readonly IGameMetadataResolver _metadataResolver;
    private readonly SemaphoreSlim _scanLock = new(1, 1);
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly object _watcherGate = new();
    private readonly object _lifecycleGate = new();
    private CancellationTokenSource? _cancellation;
    private Task? _worker;
    private long _debounceGeneration;

    public LibraryService(
        ILogger<LibraryService> logger,
        IGameMetadataResolver metadataResolver)
    {
        _logger = logger;
        _metadataResolver = metadataResolver;
    }

    public event EventHandler<LibraryGameNotification>? GameDiscovered;

    public void Start()
    {
        lock (_lifecycleGate)
        {
            if (_worker is { IsCompleted: false })
                return;

            var cancellation = new CancellationTokenSource();
            _cancellation = cancellation;
            ConfigureWatchers();

            // Xem ghi chu trong PlayTimeService.Start: worker phai bat dau tren
            // thread pool, neu khong Stop() tu menu tray se treo.
            _worker = Task.Run(() => RunAsync(cancellation.Token));
            _logger.LogInformation("Library monitoring started");
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
        DisposeWatchers();

        try
        {
            if (worker != null && !worker.Wait(StopTimeout))
                _logger.LogWarning("Library worker did not stop within {Timeout}", StopTimeout);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Library worker ended with an error");
        }

        cancellation.Dispose();
        _logger.LogInformation("Library monitoring stopped");
    }

    /// <summary>
    /// Quet ngay, khong doi het chu ky 15 phut. Dung cho muc "Quet ngay" o menu
    /// tray. Tra ve false khi service dang tat.
    /// </summary>
    public bool RequestScanNow()
    {
        var cancellation = _cancellation;
        if (cancellation == null)
            return false;

        _ = Task.Run(async () =>
        {
            try
            {
                await ScanAsync(cancellation.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Manual library scan failed");
            }
        });

        return true;
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            await ScanAsync(cancellationToken);
            using var timer = new PeriodicTimer(FullScanInterval);
            while (await timer.WaitForNextTickAsync(cancellationToken))
                await ScanAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Library worker stopped unexpectedly");
        }
    }

    private void ConfigureWatchers()
    {
        List<string> libraries;
        try
        {
            libraries = GetValidLibraries().ToList();
        }
        catch (Exception ex)
        {
            // config.json hong khong duoc lam chet Start(): tray van phai song va hai
            // service con lai van phai chay.
            _logger.LogError(ex, "Could not read the library list from config");
            return;
        }

        lock (_watcherGate)
        {
            DisposeWatchersLocked();

            foreach (var library in libraries)
            {
                try
                {
                    var watcher = new FileSystemWatcher(library, "*.exe")
                    {
                        IncludeSubdirectories = true,
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
                    };
                    watcher.Created += OnLibraryChanged;
                    watcher.Deleted += OnLibraryChanged;
                    watcher.Renamed += OnLibraryChanged;
                    watcher.Error += OnWatcherError;
                    watcher.EnableRaisingEvents = true;
                    _watchers.Add(watcher);
                }
                catch (Exception ex)
                {
                    // O dia bi rut, duong dan qua dai, het handle: bo qua thu muc do
                    // thay vi lam hong ca service.
                    _logger.LogWarning(ex, "Unable to monitor library {Library}", library);
                }
            }
        }
    }

    // Watcher tu tat khi bo dem tran hoac thu muc bien mat. Khong bat su kien nay
    // thi quet theo su kien chet im, chi con lan quet dinh ky moi 15 phut.
    private void OnWatcherError(object sender, System.IO.ErrorEventArgs e)
    {
        _logger.LogWarning(e.GetException(), "A library watcher reported an error; rebuilding watchers");

        if (_cancellation == null)
            return;

        try
        {
            ConfigureWatchers();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not rebuild the library watchers");
        }
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
                _logger.LogWarning(ex, "Could not dispose a library watcher");
            }
        }

        _watchers.Clear();
    }

    private void OnLibraryChanged(object sender, FileSystemEventArgs e)
    {
        var cancellation = _cancellation;
        if (cancellation == null)
            return;

        // So sanh "the he" thay cho mot CancellationTokenSource dung chung. Ban cu
        // huy va dispose CTS ngay trong handler, ma FileSystemWatcher goi handler
        // tu nhieu thread cung luc: mot thread dispose xong thi thread kia cham vao
        // Token cua CTS da chet -> ObjectDisposedException nem tren thread cua
        // watcher -> khong ai bat -> sap tien trinh tray.
        var generation = Interlocked.Increment(ref _debounceGeneration);

        _ = Task.Run(async () =>
        {
            try
            {
                var token = cancellation.Token;
                await Task.Delay(ChangeDebounce, token);

                if (Interlocked.Read(ref _debounceGeneration) != generation)
                    return;

                await ScanAsync(token);
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
                _logger.LogError(ex, "Debounced library scan failed");
            }
        });
    }

    private async Task ScanAsync(CancellationToken cancellationToken)
    {
        // Khong truyen token vao WaitAsync: chi can biet co lan quet khac dang chay
        // hay khong, huy service da duoc kiem tra ngay ben duoi.
        if (!await _scanLock.WaitAsync(0))
            return;

        try
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            var detected = await Task.Run(() =>
                GetValidLibraries()
                    .SelectMany(EnumerateExecutables)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(path => new DetectedProcess
                    {
                        ExeName = Path.GetFileName(path),
                        FullPath = path
                    })
                    .ToList());

            // Duyet o dia co the mat vai giay. Dung service trong luc do thi khong
            // duoc ghi gi xuong CSDL nua.
            if (cancellationToken.IsCancellationRequested)
                return;

            var current = DataService.LoadDetectedProcesses();
            var currentPaths = current.Select(x => x.FullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var detectedPaths = detected.Select(x => x.FullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (currentPaths.SetEquals(detectedPaths))
                return;

            var addedExecutables = detected
                .Where(process => !currentPaths.Contains(process.FullPath))
                .ToList();
            var notifications = await DetectNewGamesAsync(addedExecutables, cancellationToken);

            DataService.SyncDetectedProcesses(detected);
            DataService.SyncGameStatus();

            foreach (var notification in notifications)
                PublishNotification(notification);

            _logger.LogInformation("Library scan synchronized {Count} executables", detected.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Library scan failed");
        }
        finally
        {
            _scanLock.Release();
        }
    }

    private async Task<List<LibraryGameNotification>> DetectNewGamesAsync(
        IReadOnlyCollection<DetectedProcess> addedExecutables,
        CancellationToken cancellationToken)
    {
        var notifications = new List<LibraryGameNotification>();
        if (addedExecutables.Count == 0)
            return notifications;

        var games = DataService.LoadGames();
        var notifiedGameIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var executable in addedExecutables)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var existingGame = FindGameByExecutable(games, executable.ExeName);
            if (existingGame != null)
            {
                AddNotification(
                    notifications,
                    notifiedGameIds,
                    existingGame,
                    LibraryGameNotificationKind.Detected);
                continue;
            }

            GameMetadataMatch? metadataMatch;
            try
            {
                metadataMatch = await _metadataResolver.ResolveByExecutableAsync(
                    executable.FullPath,
                    cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(
                    ex,
                    "Metadata lookup failed for {ExecutablePath}",
                    executable.FullPath);
                continue;
            }

            if (metadataMatch == null)
                continue;

            existingGame = FindExistingMetadataGame(games, metadataMatch);
            if (existingGame != null)
            {
                AddNotification(
                    notifications,
                    notifiedGameIds,
                    existingGame,
                    LibraryGameNotificationKind.Detected);
                continue;
            }

            var game = PrepareMetadataGame(metadataMatch, executable.ExeName);
            if (string.IsNullOrWhiteSpace(game.Name))
            {
                _logger.LogWarning(
                    "Metadata lookup returned a game without a name for {ExecutablePath}",
                    executable.FullPath);
                continue;
            }

            // Mot game trung Id khong duoc lam hong ca lan quet: truoc day AddGame
            // nem ra ngoai la bo luon buoc dong bo DetectedProcesses ben duoi.
            try
            {
                DataService.AddGame(game);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not add the detected game {GameName}", game.Name);
                continue;
            }

            games.Add(game);
            AddNotification(
                notifications,
                notifiedGameIds,
                game,
                LibraryGameNotificationKind.Added);
        }

        return notifications;
    }

    private static Game? FindGameByExecutable(IEnumerable<Game> games, string executableName)
    {
        var normalizedExecutable = NormalizeExecutableName(executableName);
        return games.FirstOrDefault(game =>
            string.Equals(
                NormalizeExecutableName(game.ExeName),
                normalizedExecutable,
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                NormalizeExecutableName(game.InitExeName),
                normalizedExecutable,
                StringComparison.OrdinalIgnoreCase));
    }

    private static Game? FindExistingMetadataGame(
        IEnumerable<Game> games,
        GameMetadataMatch metadataMatch)
    {
        var metadataGame = metadataMatch.Game;
        if (!string.IsNullOrWhiteSpace(metadataGame.Id))
        {
            var idMatch = games.FirstOrDefault(game =>
                string.Equals(game.Id, metadataGame.Id, StringComparison.OrdinalIgnoreCase));
            if (idMatch != null)
                return idMatch;
        }

        var nameMatch = games.FirstOrDefault(game =>
            !string.IsNullOrWhiteSpace(metadataGame.Name) &&
            string.Equals(game.Name, metadataGame.Name, StringComparison.OrdinalIgnoreCase));
        if (nameMatch != null)
            return nameMatch;

        return metadataMatch.ExecutableNames
            .Select(executableName => FindGameByExecutable(games, executableName))
            .FirstOrDefault(game => game != null);
    }

    private static Game PrepareMetadataGame(
        GameMetadataMatch metadataMatch,
        string detectedExecutableName)
    {
        var game = metadataMatch.Game;
        if (string.IsNullOrWhiteSpace(game.Id))
            game.Id = Guid.NewGuid().ToString();

        var primaryExecutable = metadataMatch.ExecutableNames.FirstOrDefault(name =>
            string.Equals(
                NormalizeExecutableName(name),
                NormalizeExecutableName(detectedExecutableName),
                StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(game.ExeName))
            game.ExeName = NormalizeExecutableName(primaryExecutable ?? detectedExecutableName);
        else
            game.ExeName = NormalizeExecutableName(game.ExeName);

        game.InitExeName = NormalizeExecutableName(game.InitExeName);
        game.Ready = false;
        game.CreateAt ??= DateTime.Now;
        return game;
    }

    private static void AddNotification(
        ICollection<LibraryGameNotification> notifications,
        ISet<string> notifiedGameIds,
        Game game,
        LibraryGameNotificationKind kind)
    {
        var identity = string.IsNullOrWhiteSpace(game.Id) ? game.Name : game.Id;
        if (!notifiedGameIds.Add(identity))
            return;

        notifications.Add(new LibraryGameNotification(kind, game.Name));
    }

    private static string NormalizeExecutableName(string? executableName)
    {
        if (string.IsNullOrWhiteSpace(executableName))
            return string.Empty;

        var fileName = Path.GetFileName(executableName.Trim());
        return fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? fileName[..^4]
            : fileName;
    }

    private void PublishNotification(LibraryGameNotification notification)
    {
        var handlers = GameDiscovered?.GetInvocationList()
            .Cast<EventHandler<LibraryGameNotification>>() ?? [];

        foreach (var handler in handlers)
        {
            try
            {
                handler(this, notification);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "A library notification handler failed");
            }
        }
    }

    private static IEnumerable<string> GetValidLibraries()
    {
        return (JsonService.LoadConfig().GameLibraryPath ?? [])
            .Where(path => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> EnumerateExecutables(string library)
    {
        try
        {
            return Directory.EnumerateFiles(library, "*.exe", ScanOptions).ToArray();
        }
        catch
        {
            return [];
        }
    }

    public void Dispose()
    {
        Stop();
        _scanLock.Dispose();
    }
}
