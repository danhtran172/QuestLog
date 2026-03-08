using MasterPlay.Models;
using MasterPlay.Models.Models;
using Microsoft.Extensions.Logging;

namespace MasterPlay.TrayApp.Services
{
    // LibraryService
    public class LibraryService
    {
        // Fields
        private readonly ILogger<LibraryService> _logger;
        private System.Timers.Timer? _scanTimer;

        // Constructor
        public LibraryService(ILogger<LibraryService> logger)
        {
            _logger = logger;
        }

        // Public Methods
        public void Start()
        {
            _logger.LogInformation("LIBRARY SERVICE STARTED");

            Task.Run(() => ScanLibrary());

            _scanTimer = new System.Timers.Timer(60000);
            _scanTimer.Elapsed += (s, e) => ScanLibrary();
            _scanTimer.AutoReset = true;
            _scanTimer.Start();
        }

        public void Stop()
        {
            _scanTimer?.Stop();
            _logger.LogInformation("Library Scanner Service stopped.");
        }

        // Private Methods
        private void ScanLibrary()
        {
            try
            {
                var libraries = JsonService.LoadConfig().GameLibraryPath;
                foreach (var library in libraries)
                {
                    if (string.IsNullOrEmpty(library) || !Directory.Exists(library))
                    {
                        _logger.LogWarning("Invalid library path: {path}", library);
                        return;
                    }

                    _logger.LogInformation("Scanning library: {path}", library);

                    var detectedProcesses = Directory.GetFiles(library, "*.exe", SearchOption.AllDirectories);
                    var newProcessesList = new List<DetectedProcess>();

                    foreach (var file in detectedProcesses)
                    {
                        string fileName = Path.GetFileName(file);
                        newProcessesList.Add(new DetectedProcess
                        {
                            ExeName = fileName,
                            FullPath = file,
                        });
                    }

                    var currentProcesses = DataService.LoadDetectedProcesses();
                    var currentSet = new HashSet<string>(currentProcesses.Select(p => p.FullPath));
                    var newSet = new HashSet<string>(newProcessesList.Select(p => p.FullPath));

                    bool hasChanges = !currentSet.SetEquals(newSet);

                    if (hasChanges)
                    {
                        DataService.SyncDetectedProcesses(newProcessesList);
                        DataService.SyncGameStatus();

                        try
                        {
                            var games = DataService.LoadGames().Where(g => !g.IsDisabled && g.Ready).ToList();
                            foreach (var game in games)
                            {
                                if (!string.IsNullOrWhiteSpace(game.BackupFolderPath) && Directory.Exists(game.BackupFolderPath))
                                    continue;

                                DataService.CreateBackupFolder(game);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error creating backup folders after library scan.");
                        }

                        _logger.LogInformation("Library scan completed and updated. Found {count} executables.", newProcessesList.Count);
                    }
                    else
                    {
                        _logger.LogInformation("Library scan completed. No changes detected.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during library scanning.");
            }
        }
    }
}