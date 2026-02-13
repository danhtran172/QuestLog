using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GenerativeAI.Types;
using MasterPlay.Models;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Xml.Linq;

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

        public event Action<bool>? RequestClose;

        public AddGameViewModel()
        {
            // Can load defaults if needed
        }

        [RelayCommand]
        private async Task Sync()
        {
            string name = GameName.Trim();
            if (string.IsNullOrEmpty(name))
            {
                StatusMessage = "Please enter the game name first.";
                return;
            }

            IsSyncing = true;
            StatusMessage = "Syncing... Please wait.";

            try
            {
                // TODO: Uncomment when you have API keys
                // var (exe, save) = await GetGameInfoFromGemini(name);
                // ExeName = exe ?? ExeName;
                // SaveLocation = save ?? SaveLocation;

                // string imgUrl = await GetCoverImageFromRawg(name);
                // if (!string.IsNullOrEmpty(imgUrl))
                // {
                //     ImagePath = imgUrl;
                //     LoadpreviewImage(imgUrl);
                // }

                StatusMessage = "Sync completed!";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Sync failed: {ex.Message}";
            }
            finally
            {
                IsSyncing = false;
            }
        }

        [RelayCommand]
        private void AddGame()
        {
            string name = GameName.Trim();
            string exe = ExeName.Trim();
            string saveLoc = SaveLocation.Trim();


            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(exe) || string.IsNullOrEmpty(saveLoc))
            {
                StatusMessage = "All required fields must be filled.";
                return;
            }

            try
            {
                var games = ConfigManager.LoadGames();

                // Check duplicate name (case insensitive)
                if (games.Exists(g => g.Name.Trim().Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    StatusMessage = "This Game name already exists.";
                    return;
                }

                var config = ConfigManager.LoadConfig();
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

                string gameFolder = Path.Combine(backupRoot, newGame.Name);
                newGame.BackupFolderPath = gameFolder;

                var game = InputFormater(newGame);


                games.Add(game);
                ConfigManager.SaveGamesData(games);

                Directory.CreateDirectory(gameFolder);

                string txtPath = Path.Combine(gameFolder, "SaveFileLocation.txt");
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
                var original = ConfigManager.LoadGames().FirstOrDefault(g => g.Id == game.Id);
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

            // 3. Clean Paths: Xóa dấu ngoặc kép và khoảng trắng thừa
            game.SaveFolderPath = game.SaveFolderPath?.Trim('"').Trim() ?? string.Empty;
            game.BackupFolderPath = game.BackupFolderPath?.Trim('"').Trim() ?? string.Empty;
            game.ImagePath = game.ImagePath?.Trim('"').Trim() ?? string.Empty;

            return game;
        } }
    }