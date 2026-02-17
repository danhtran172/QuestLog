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
            _logger.LogInformation("Library Scanner Service started.");

            // Quét ngay lập tức khi start
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
                Data.SyncReadyGames();
                Data.SortGames();

                var config = Data.LoadConfig();
                var libraries = config.GameLibraryPath;
                foreach (var library in libraries)
                {
                    if (string.IsNullOrEmpty(library) || !Directory.Exists(library))
                    {
                        _logger.LogWarning("LibraryPath not found or invalid: {path}", library);
                        return;
                    }

                    _logger.LogInformation("Scanning library for .exe files in: {path}", library);

                    var exeFiles = Directory.GetFiles(library, "*.exe", SearchOption.AllDirectories);

                    var existingGames = Data.LoadAvailableGames();
                    var newList = new List<DetectedGame>();

                    foreach (var file in exeFiles)
                    {
                        string fileName = Path.GetFileName(file);
                        var existing = existingGames.FirstOrDefault(g => g.FullPath.Equals(file, StringComparison.OrdinalIgnoreCase));

                        newList.Add(new DetectedGame
                        {
                            ExeName = fileName,
                            FullPath = file,
                        });
                    }

                    // 3. Lưu lại vào availableGames.json
                    Data.SaveAvailableGames(newList);
                    _logger.LogInformation("Library scan completed. Found {count} process.", newList.Count);
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