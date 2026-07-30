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
    private CancellationTokenSource? _cancellation;
    private CancellationTokenSource? _debounceCancellation;
    private Task? _worker;

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
        if (_worker is { IsCompleted: false })
            return;

        _cancellation = new CancellationTokenSource();
        ConfigureWatchers();
        _worker = RunAsync(_cancellation.Token);
        _logger.LogInformation("Library monitoring started");
    }

    public void Stop()
    {
        _cancellation?.Cancel();
        _debounceCancellation?.Cancel();

        try
        {
            _worker?.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }

        foreach (var watcher in _watchers)
            watcher.Dispose();
        _watchers.Clear();
        _cancellation?.Dispose();
        _debounceCancellation?.Dispose();
        _cancellation = null;
        _debounceCancellation = null;
        _worker = null;
        _logger.LogInformation("Library monitoring stopped");
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        await ScanAsync(cancellationToken);
        using var timer = new PeriodicTimer(FullScanInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken))
            await ScanAsync(cancellationToken);
    }

    private void ConfigureWatchers()
    {
        foreach (var library in GetValidLibraries())
        {
            var watcher = new FileSystemWatcher(library, "*.exe")
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
            };
            watcher.Created += OnLibraryChanged;
            watcher.Deleted += OnLibraryChanged;
            watcher.Renamed += OnLibraryChanged;
            watcher.EnableRaisingEvents = true;
            _watchers.Add(watcher);
        }
    }

    private void OnLibraryChanged(object sender, FileSystemEventArgs e)
    {
        var parentToken = _cancellation?.Token ?? CancellationToken.None;
        _debounceCancellation?.Cancel();
        _debounceCancellation?.Dispose();
        _debounceCancellation = CancellationTokenSource.CreateLinkedTokenSource(parentToken);
        var token = _debounceCancellation.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(ChangeDebounce, token);
                await ScanAsync(token);
            }
            catch (OperationCanceledException)
            {
            }
        }, token);
    }

    private async Task ScanAsync(CancellationToken cancellationToken)
    {
        if (!await _scanLock.WaitAsync(0, cancellationToken))
            return;

        try
        {
            var detected = await Task.Run(() =>
                GetValidLibraries()
                    .SelectMany(EnumerateExecutables)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(path => new DetectedProcess
                    {
                        ExeName = Path.GetFileName(path),
                        FullPath = path
                    })
                    .ToList(), cancellationToken);

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

            foreach (var game in DataService.LoadActiveGames())
            {
                if (string.IsNullOrWhiteSpace(game.BackupFolderPath) || !Directory.Exists(game.BackupFolderPath))
                    DataService.CreateBackupFolder(game);
            }

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

            DataService.AddGame(game);
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
