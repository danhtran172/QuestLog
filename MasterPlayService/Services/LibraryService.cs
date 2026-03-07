using MasterPlay.Models;
using MasterPlay.Models.Models;
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

                    // Lấy danh sách hiện tại từ DB
                    var currentProcesses = DataService.LoadDetectedProcesses();

                    // So sánh (cần so sánh dựa trên FullPath vì nó unique)
                    var currentSet = new HashSet<string>(currentProcesses.Select(p => p.FullPath));
                    var newSet = new HashSet<string>(newProcessesList.Select(p => p.FullPath));

                    bool hasChanges = !currentSet.SetEquals(newSet);

                    if (hasChanges)
                    {
                        DataService.SyncDetectedProcesses(newProcessesList);
                        DataService.SyncGameStatus();
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

        public void Stop()
        {
            _scanTimer?.Stop();
            _logger.LogInformation("Library Scanner Service stopped.");
        }
    }
}