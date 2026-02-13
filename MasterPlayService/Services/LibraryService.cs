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
                var config = ConfigManager.LoadConfig();
                var library = config.GameLibraryPath;
                if (string.IsNullOrEmpty(library) || !Directory.Exists(library))
                {
                    _logger.LogWarning("LibraryPath not found or invalid: {path}", library);
                    return;
                }

                _logger.LogInformation("Scanning library for .exe files in: {path}", library);

                // 1. Tìm tất cả file .exe (quét cả thư mục con)
                var exeFiles = Directory.GetFiles(library, "*.exe", SearchOption.AllDirectories);

                // 2. Load danh sách cũ để giữ lại trạng thái Ignore/InUse
                var existingGames = ConfigManager.LoadAvailableGames();
                var newList = new List<DetectedGame>();

                foreach (var file in exeFiles)
                {
                    string fileName = Path.GetFileName(file);
                    var existing = existingGames.FirstOrDefault(g => g.FullPath.Equals(file, StringComparison.OrdinalIgnoreCase));

                    newList.Add(new DetectedGame
                    {
                        ExeName = fileName,
                        FullPath = file,
                        // Nếu đã có trong danh sách cũ thì giữ nguyên Ignore, nếu chưa thì mặc định false
                        Ignore = existing?.Ignore ?? false,
                        // Kiểm tra xem process này có đang chạy không
                        InUse = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(fileName)).Length > 0
                    });
                }

                // 3. Lưu lại vào availableGames.json
                ConfigManager.SaveAvailableGames(newList);
                _logger.LogInformation("Library scan completed. Found {count} process.", newList.Count);
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