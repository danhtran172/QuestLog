using MasterPlay.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace MasterPlay.Service.Services
{
    public class LibraryService
    {
        private readonly ILogger<LibraryService> _logger;
        private System.Timers.Timer? _scanTimer;

        public LibraryService(ILogger<LibraryService> logger)
        {
            _logger = logger;
        }

        public void Start(CancellationToken stoppingToken)
        {
            _logger.LogInformation("LIBRARY SERVICE STARTED");

            Task.Run(() => ScanLibrary());

            _scanTimer = new System.Timers.Timer(60000);
            _scanTimer.Elapsed += (s, e) => ScanLibrary();
            _scanTimer.AutoReset = true;
            _scanTimer.Start();
        }

        private void ScanLibrary()
        {
            try
            {
                DataService.SyncReadyGames();
                DataService.SortGames();

                var config = DataService.LoadConfig();
                var libraries = config.GameLibraryPath;
                foreach (var library in libraries)
                {
                    if (string.IsNullOrEmpty(library) || !Directory.Exists(library))
                    {
                        _logger.LogWarning("Invalid library path: {path}", library);
                        return;
                    }

                    _logger.LogInformation("Scanning library: {path}", library);

                    var exeFiles = Directory.GetFiles(library, "*.exe", SearchOption.AllDirectories);
                    var existingGames = DataService.LoadAvailableGames();
                    var newList = new List<DetectedGame>();

                    foreach (var file in exeFiles)
                    {
                        string fileName = Path.GetFileName(file);
                        newList.Add(new DetectedGame
                        {
                            ExeName = fileName,
                            FullPath = file,
                        });
                    }

                    DataService.SaveAvailableGames(newList);
                    _logger.LogInformation("Library scan completed. Found {count} executables.", newList.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during library scanning.");
            }
        }

        public void Stop()
        {
            _scanTimer?.Stop();
            _logger.LogInformation("Library Scanner Service stopped.");
        }
    }
}