using MasterPlay.Models;
using MasterPlay.Models.Models;
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
    private readonly SemaphoreSlim _scanLock = new(1, 1);
    private readonly List<FileSystemWatcher> _watchers = [];
    private CancellationTokenSource? _cancellation;
    private CancellationTokenSource? _debounceCancellation;
    private Task? _worker;

    public LibraryService(ILogger<LibraryService> logger)
    {
        _logger = logger;
    }

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

            DataService.SyncDetectedProcesses(detected);
            DataService.SyncGameStatus();

            foreach (var game in DataService.LoadActiveGames())
            {
                if (string.IsNullOrWhiteSpace(game.BackupFolderPath) || !Directory.Exists(game.BackupFolderPath))
                    DataService.CreateBackupFolder(game);
            }

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
