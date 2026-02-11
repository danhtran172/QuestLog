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
                statusMessage = "Please enter the game name first.";
                return;
            }

            IsSyncing = true;
            statusMessage = "Syncing... Please wait.";

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

                statusMessage = "Sync completed!";
            }
            catch (Exception ex)
            {
                statusMessage = $"Sync failed: {ex.Message}";
            }
            finally
            {
                IsSyncing = false;
            }
        }

        [RelayCommand]
        private void AddGame()
        {
            MessageBox.Show("Button Clicked");
            string name = GameName.Trim();
            string exe = ExeName.Trim();
            string saveLoc = SaveLocation.Trim();

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(exe) || string.IsNullOrEmpty(saveLoc))
            {
                statusMessage = "All required fields must be filled.";
                return;
            }

            try
            {
                var games = ConfigManager.LoadGames();

                // Check duplicate name (case insensitive)
                if (games.Exists(g => g.Name.Trim().Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    statusMessage = "Game name already exists.";
                    return;
                }

                var config = ConfigManager.LoadConfig();
                string backupRoot = config.BackupRootPath;

                if (string.IsNullOrEmpty(backupRoot))
                {
                    statusMessage = "Backup root path is not set in config.";
                    return;
                }

                var newGame = new Game
                {
                    Name = name,
                    ExeName = exe,
                    SaveFilePath = saveLoc,
                    ImagePath = ImagePath.Trim(),
                    TotalPlayTimeSeconds = 0,
                    IsDisabled = false,
                };

                string gameFolder = Path.Combine(backupRoot, newGame.Name);
                newGame.BackupFolderPath = gameFolder;

                games.Add(newGame);
                ConfigManager.SaveGamesData(games);

                Directory.CreateDirectory(gameFolder);

                string txtPath = Path.Combine(gameFolder, "SaveFileLocation.txt");
                File.WriteAllText(txtPath, newGame.SaveFilePath);

                statusMessage = "Game added successfully!";
                RequestClose?.Invoke(true); // Success → close window
            }
            catch (Exception ex)
            {
                statusMessage = $"Error: {ex.Message}";
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
                previewImage = null;
                return;
            }

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(url, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                previewImage = bitmap;
            }
            catch
            {
                previewImage = null;
            }
        }

        partial void OnImagePathChanged(string value)
        {
            LoadpreviewImage(value);
        }
    }
}