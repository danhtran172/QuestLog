using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GenerativeAI.Types;
using MasterPlay.Models;
using MasterPlay.Models.Models;
using MasterPlay.UI.ViewModels.Generral;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace MasterPlay.UI.ViewModels
{
    public partial class AddGameViewModel : BaseViewModel
    {
        // --- Properties ---
        [ObservableProperty]
        private string gameName = string.Empty;
        [ObservableProperty]
        private string exeName = string.Empty;
        [ObservableProperty]
        private string saveLocation = string.Empty;
        [ObservableProperty]
        private string genre = string.Empty;
        [ObservableProperty]
        private DateOnly? releaseDate = null;
        [ObservableProperty]
        private string developer = string.Empty;
        [ObservableProperty]
        private string publisher = string.Empty;

        [ObservableProperty]
        private string imagePath = string.Empty;
        [ObservableProperty]
        private BitmapImage? previewImage;
        [ObservableProperty]
        private bool isSyncing = false;
        [ObservableProperty]
        private bool isBusy;
        [ObservableProperty]
        private Selections selections = LoadSelections();

        private static Selections LoadSelections()
        {
            return DataService.LoadSelection();
        }

        // --- Constructors and Initialization ---
        public AddGameViewModel()
        {
        }

        // --- Commands ---
        [RelayCommand]
        private async Task Sync()
        {
            if (string.IsNullOrWhiteSpace(GameName)) return;
            IsBusy = true;
            try
            {
                var rawgGame = await ApiService.GetMetadata_Rawg(GameName);
             

                if (rawgGame != null)
                {
                    GameName = rawgGame.Name;
                    ImagePath = rawgGame.Background_Image;

                    Genre = rawgGame.Genres;
                    ReleaseDate = ApiService.ParseDate(rawgGame.ReleaseDate);
                    developer = rawgGame.Developers;
                    Publisher = rawgGame.Publishers;

                }
                var metaData = await DataService.FindGameMetaData(GameName);
                ExeName = metaData.Exe;
                SaveLocation = metaData.SaveFolder;
                StatusMessage = "Sync completed!";
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
                var games = DataService.LoadGames();
                // Check duplicate name (case insensitive)
                if (games.Exists(g => g.Name.Trim().Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    StatusMessage = "This Game name already exists.";
                    return;
                }
                var config = DataService.LoadConfig();
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
                string backupFolder = Path.Combine(backupRoot, ViewModelHelpers.ValidFolderName(name));
                newGame.BackupFolderPath = backupFolder;
                ViewModelHelpers.InputFormatter(newGame);
                games.Add(newGame);
                DataService.SaveGamesData(games);
                Directory.CreateDirectory(backupFolder);
                string txtPath = Path.Combine(backupFolder, "SaveFileLocation.txt");
                File.WriteAllText(txtPath, newGame.SaveFolderPath);
                StatusMessage = "Game added successfully!";
                RaiseRequestClose(true); // Success → close window
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            RaiseRequestClose(false);
        }

        // --- Private Methods: Image Handling ---
        partial void OnImagePathChanged(string value)
        {
            PreviewImage = ViewModelHelpers.LoadPreviewImage(value);
        }
    }
}