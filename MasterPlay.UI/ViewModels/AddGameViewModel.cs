using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MasterPlay.Helpers;
using MasterPlay.Models;
using MasterPlay.Models.Models;
using MasterPlay.UI.ViewModels.Generral;
using Steamworks;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace MasterPlay.UI.ViewModels
{
    public partial class AddGameViewModel : BaseViewModel
    {
        // Token dùng để hủy việc xóa message nếu có message mới đè lên
        private CancellationTokenSource? _msgCts;

        // ==================== PROPERTIES ====================
        [ObservableProperty]
        private string gameName = string.Empty;
        [ObservableProperty]
        private string slugName = string.Empty;

        [ObservableProperty]
        private string exeName = string.Empty;

        [ObservableProperty]
        private string initExeName = string.Empty;

        [ObservableProperty]
        private string saveLocation = string.Empty;

        [ObservableProperty]
        private string backupFolderPath = string.Empty;

        [ObservableProperty]
        private string imagePath = string.Empty;
        [ObservableProperty]
        private string galleryImage = string.Empty;

        [ObservableProperty]
        private BitmapImage? previewImage;
     

        [ObservableProperty]
        private DateOnly? releaseDate;

        [ObservableProperty]
        private string publisher = string.Empty;

        [ObservableProperty]
        private string developer = string.Empty;

        [ObservableProperty]
        private string series = string.Empty;

        [ObservableProperty]
        private string genre = string.Empty;

        [ObservableProperty]
        private string theme = string.Empty;

        [ObservableProperty]
        private float score;

        [ObservableProperty]
        private int playTimeHours;

        [ObservableProperty]
        private int playTimeMinutes;

        [ObservableProperty]
        private int adjustMinutes;

        [ObservableProperty]
        private long avgPlaytime;

        [ObservableProperty]
        private int metaScore;

        [ObservableProperty]
        private float excited;

        [ObservableProperty]
        private string ownership = string.Empty;

        [ObservableProperty]
        private string progress = string.Empty;

        [ObservableProperty]
        private string finishedIn = string.Empty;

        [ObservableProperty]
        private Selections selections = LoadSelections();

        [ObservableProperty]
        private string statusMessage = string.Empty;

        [ObservableProperty]
        public string publishDateInput = string.Empty;

        [ObservableProperty]
        private bool isBusy;

        private static Selections LoadSelections()
        {
            return DataService.LoadSelection();
        }

        // ==================== CONSTRUCTOR ====================
        public AddGameViewModel()
        {
            // Thiết lập giá trị mặc định
            Ownership = "Not Owned";
            Progress = "Pending";

            var defaultOwnership = selections.Ownership?.FirstOrDefault(x => x.Equals("Not Owned", StringComparison.OrdinalIgnoreCase));
            if (defaultOwnership != null) Ownership = defaultOwnership;

            var defaultProgress = selections.Progress?.FirstOrDefault(x => x.Equals("Pending", StringComparison.OrdinalIgnoreCase));
            if (defaultProgress != null) Progress = defaultProgress;
        }

        // ==================== HELPERS ====================

        private async Task ShowStatus(string message, int delayMs = 3000)
        {
            _msgCts?.Cancel();
            _msgCts = new CancellationTokenSource();
            var token = _msgCts.Token;

            StatusMessage = message;

            try
            {
                await Task.Delay(delayMs, token);
                if (!token.IsCancellationRequested)
                {
                    StatusMessage = string.Empty;
                }
            }
            catch (TaskCanceledException) { }
        }

        // ==================== COMMANDS ====================
        [RelayCommand]
        private async Task Sync()
        {
            if (string.IsNullOrWhiteSpace(GameName))
            {
                await ShowStatus("Game name is required.");
                return;
            }

            IsBusy = true;
            StatusMessage = "Syncing...";

            try
            {
                var rawgGame = await ApiService.GetMetadata_Rawg(GameName);

                if (rawgGame != null)
                {
                    GameName = rawgGame.Name ?? GameName;
                    ImagePath = rawgGame.Background_Image ?? ImagePath;
                    PreviewImage = ViewModelHelpers.LoadPreviewImage(ImagePath);
                    GalleryImage = rawgGame.Background_Image ?? ImagePath;
                    Publisher = rawgGame.Publishers ?? string.Empty;
                    SlugName = rawgGame.SlugName ?? string.Empty; 
                    Developer = rawgGame.Developers ?? string.Empty;
                    Genre = rawgGame.Genres ?? string.Empty;

                    SyncPublishDate(ApiService.StringtoDate(rawgGame.ReleaseDate));
                    MetaScore = rawgGame.MetaScore;

                    var gallerryImg = await ApiService.GetGalleryImage(GameName);
                    
                    if (gallerryImg != null)
                    {
                        GalleryImage = gallerryImg;
                    }
                    await ShowStatus("Sync successful");
                }
                else
                {
                    await ShowStatus("No data found for this game.");
                }



            }
            catch (Exception ex)
            {
                await ShowStatus($"Sync failed: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void SyncPublishDate(DateOnly? date)
        {
            PublishDateInput = date?.ToString("dd/MM/yyyy") ?? "";
        }

        [RelayCommand]
        private async Task SyncImage()
        {
            if (string.IsNullOrWhiteSpace(ImagePath))
            {
                PreviewImage = null;
                await ShowStatus("No image URL provided.");
                return;
            }

            try
            {
                PreviewImage = ViewModelHelpers.LoadPreviewImage(ImagePath);
                await ShowStatus("Image preview updated.");
            }
            catch (Exception ex)
            {
                await ShowStatus($"Failed to load image: {ex.Message}");
            }
        }

        [RelayCommand]
        private void EditSaveFolder()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Save Folder",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            if (dialog.ShowDialog() == true)
            {
                SaveLocation = dialog.FolderName;
            }
        }

        [RelayCommand]
        private void EditBackupFolder()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Backup Folder",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            if (dialog.ShowDialog() == true)
            {
                BackupFolderPath = dialog.FolderName;
            }
        }


        [RelayCommand]
        private async Task CheckPlayTime()
        {
            if (string.IsNullOrWhiteSpace(GameName)) return;

            string url = "https://howlongtobeat.com/?q=" + Uri.EscapeDataString(GameName);
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                await ShowStatus($"Cannot open browser: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task AddGame()
        {
            if (string.IsNullOrWhiteSpace(GameName))
            {
                await ShowStatus("Game Name is Required");
                return;
            }
            if (string.IsNullOrWhiteSpace(ImagePath))
            {
                await ShowStatus("Game Cover is Required");
                return;
            }

            try
            {
                var games = DataService.LoadGames().Where(g => !g.IsDisabled).ToList();

                if (games.Exists(g => g.Name.Trim().Equals(GameName.Trim(), StringComparison.OrdinalIgnoreCase)))
                {
                    await ShowStatus("Game name already exists.");
                    return;
                }

                var config = DataService.LoadConfig();
                if (string.IsNullOrEmpty(config.BackupRootPath))
                {
                    await ShowStatus("Backup root path not set.");
                    return;
                }

                var newGame = new Game
                {
                    Name = GameName.Trim(),
                    SlugName = SlugName.Trim(),
                    ExeName = ExeName.Trim(),
                    InitExeName = InitExeName?.Trim(),
                    SaveFolderPath = SaveLocation.Trim(),
                    ImagePath = ImagePath.Trim(),
                    GImagePath = GalleryImage.Trim(),
                    PublishDate = ViewModelHelpers.StringToDateOnly(PublishDateInput),
                    Publisher = Publisher.Trim(),
                    Developer = Developer.Trim(),
                    Series = Series.Trim(),
                    Genre = Genre.Trim(),
                    Theme = Theme.Trim(),
                    Score = Score,
                    TotalPlayTimeSeconds = (long)(PlayTimeHours * 3600 + PlayTimeMinutes * 60),
                    MetaScore = MetaScore,
                    AvgPlayTime = AvgPlaytime,
                    Excited = Excited,
                    Ownership = Ownership,
                    Progress = Progress,
                    FinishedIn = FinishedIn,
                    IsDisabled = false
                  
                };

                if (string.IsNullOrWhiteSpace(InitExeName))
                {
                    newGame.InitExeName = ExeName;
                }

                string validFolderName = ViewModelHelpers.ValidFolderName(newGame.Name);
                string backupFolder = Path.Combine(config.BackupRootPath, validFolderName);
                Directory.CreateDirectory(backupFolder);
                newGame.BackupFolderPath = backupFolder;

                newGame = ViewModelHelpers.InputFormatter(newGame);
                string txtPath = Path.Combine(backupFolder, "save_location.txt");
                File.WriteAllText(txtPath, newGame.SaveFolderPath);

                games.Add(newGame);
                DataService.SaveGamesData(games);

                RaiseRequestClose(true);
            }
            catch (Exception ex)
            {
                await ShowStatus($"Error: {ex.Message}");
            }
        }

        [RelayCommand]
        private void Cancel() => RaiseRequestClose(false);

        // ==================== PARTIAL CHANGE HANDLERS ====================
        partial void OnImagePathChanged(string value)
        {
            PreviewImage = ViewModelHelpers.LoadPreviewImage(value);
        }

        partial void OnPlayTimeHoursChanged(int value)
        {
            if (value < 0) PlayTimeHours = 0;
        }

        partial void OnPlayTimeMinutesChanged(int value)
        {
            if (value < 0) PlayTimeMinutes = 0;
            if (value >= 60)
            {
                PlayTimeHours += value / 60;
                PlayTimeMinutes = value % 60;
            }
        }
    }
}