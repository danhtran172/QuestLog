using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GenerativeAI.Types;
using MasterPlay.Models;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading; // Add! Để dùng DispatcherTimer

namespace MasterPlay.UI.ViewModels
{
    public partial class AddGameViewModel : ObservableObject
    {
        [ObservableProperty]
        private string gameName = string.Empty;

        [ObservableProperty]
        private string exeName = string.Empty;

        [ObservableProperty]
        private string saveLocation = string.Empty;

        [ObservableProperty]
        private string imagePath = string.Empty;

        [ObservableProperty]
        private BitmapImage? previewImage;

        [ObservableProperty]
        private string statusMessage = string.Empty;

        [ObservableProperty]
        private bool isSyncing = false;

        [ObservableProperty]
        private bool isBusy;

        public event Action<bool>? RequestClose;

        // Add! Timer để tự động xóa StatusMessage sau 3 giây
        private readonly DispatcherTimer _statusTimer;

        public AddGameViewModel()
        {
            // Khởi tạo timer một lần
            _statusTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _statusTimer.Tick += (s, e) =>
            {
                StatusMessage = string.Empty;
                _statusTimer.Stop();
            };
        }

        // Add! Khi StatusMessage thay đổi → tự động chạy timer để xóa sau 3s
        partial void OnStatusMessageChanged(string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                _statusTimer.Stop(); // Dừng timer cũ nếu đang chạy
                _statusTimer.Start(); // Bắt đầu lại 3 giây
            }
        }

        [RelayCommand]
        private async Task Sync()
        {
            if (string.IsNullOrWhiteSpace(GameName)) return;
            IsBusy = true;
            try
            {
                GameName = await ApiHelper.GetGameFormatedNameAsync(GameName);
                ExeName = await Data.FindClosestGameExeAsync(GameName) ?? "Not Found";
                ImagePath = await ApiHelper.GetGameImageUrlAsync(GameName);
                StatusMessage = "Sync completed!"; // Add! Message khi sync thành công
            }
            catch (Exception ex)
            {
                StatusMessage = $"Sync failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void AddGame()
        {
            string name = GameName.Trim();
            string exe = ExeName.Trim();
            if (exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                exe = exe[..^4].Trim();
            }
            string saveLoc = SaveLocation?.Trim('"').Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(exe) || string.IsNullOrEmpty(saveLoc))
            {
                StatusMessage = "All required fields must be filled.";
                return;
            }

            try
            {
                var games = Data.LoadGames();
                // Check duplicate name (case insensitive)
                if (games.Exists(g => g.Name.Trim().Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    StatusMessage = "This Game name already exists.";
                    return;
                }

                var config = Data.LoadConfig();
                string backupRoot = config.BackupRootPath;
                if (string.IsNullOrEmpty(backupRoot))
                {
                    StatusMessage = "Backup root path is not set in config.";
                    return;
                }

                var newGame = new Game
                {
                    Name = name,
                    ExeName = exe,
                    SaveFolderPath = saveLoc,
                    ImagePath = ImagePath.Trim(),
                    TotalPlayTimeSeconds = 0,
                    IsDisabled = false,
                };

                string backupFolder = Path.Combine(backupRoot, ValidFolderName(name));
                newGame.BackupFolderPath = backupFolder;

                var game = InputFormater(newGame);
                games.Add(game);
                Data.SaveGamesData(games);
                Directory.CreateDirectory(backupFolder);
                string txtPath = Path.Combine(backupFolder, "SaveFileLocation.txt");
                File.WriteAllText(txtPath, newGame.SaveFolderPath);

                StatusMessage = "Game added successfully!";
                RequestClose?.Invoke(true); // Success → close window
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            RequestClose?.Invoke(false);
        }

        private void LoadpreviewImage(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                PreviewImage = null;
                return;
            }
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(url, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                PreviewImage = bitmap;
            }
            catch
            {
                PreviewImage = null;
            }
        }

        partial void OnImagePathChanged(string value)
        {
            LoadpreviewImage(value);
        }

        private Game? InputFormater(Game game)
        {
            // 1. Xử lý Tên Game: Nếu trống thì lấy từ Config cũ
            if (string.IsNullOrWhiteSpace(game.Name))
            {
                var original = Data.LoadGames().FirstOrDefault(g => g.Id == game.Id);
                game.Name = original?.Name ?? "Unknown Game";
            }
            // 2. ExeName: Tự xóa đuôi .exe
            if (!string.IsNullOrWhiteSpace(game.ExeName))
            {
                string trimmed = game.ExeName.Trim();
                if (trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    game.ExeName = trimmed[..^4].Trim();
                }
            }
            game.SaveFolderPath = game.SaveFolderPath?.Trim('"').Trim() ?? string.Empty;
            game.BackupFolderPath = game.BackupFolderPath?.Trim('"').Trim() ?? string.Empty;
            game.ImagePath = game.ImagePath?.Trim('"').Trim() ?? string.Empty;
            return game;
        }

        public static string ValidFolderName(string GameName)
        {
            char[] invalid = { '<', '>', ':', '"', '/', '\\', '|', '?', '*' };
            string result = new string(GameName.Select(c => invalid.Contains(c) ? '-' : c).ToArray());
            result = Regex.Replace(result, "-+", "-").Trim('-', ' ');
            if (result.Length > 200) result = result.Substring(0, 200).TrimEnd('-');
            return result;
        }
    }
}