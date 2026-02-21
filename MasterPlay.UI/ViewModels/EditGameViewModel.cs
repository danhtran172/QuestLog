using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GenerativeAI;
using GenerativeAI.Types;
using MasterPlay.Helpers;
using MasterPlay.Models;
using MasterPlay.Models.Models;
using MasterPlay.UI.ViewModels.Generral;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Imaging;

namespace MasterPlay.UI.ViewModels
{
    public partial class EditGameViewModel : BaseViewModel
    {
        // =====================================================================
        // Constructors & Initialization
        // =====================================================================

        public EditGameViewModel() : this(new Game()) { }

        public EditGameViewModel(Game selectedGame)
        {
            Game = selectedGame ?? new Game();
            ReloadGameFromConfig();
            DataService.AssignDefaultColors();
        }

        private static Selections LoadSelections()
        {
            return DataService.LoadSelection();
        }

        // =====================================================================
        // Observable Properties (Core Data)
        // =====================================================================

        [ObservableProperty]
        private Game game;

        [ObservableProperty]
        private string backupFolderPath = string.Empty;

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
        private float excited;

        [ObservableProperty]
        private string finishedIn = string.Empty;

        [ObservableProperty]
        private long avgPlaytime;

        [ObservableProperty]
        private int metaScore;

        [ObservableProperty]
        private DateOnly? publishDate;

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private BitmapImage? previewImage;

        [ObservableProperty]
        private bool isReadOnly = true;

        [ObservableProperty]
        private bool saveFolderAccessible;

        [ObservableProperty]
        private bool backupFolderAccessible;

        // Play Time related
        [ObservableProperty]
        private int playTimeHours;

        [ObservableProperty]
        private int playTimeMinutes;

        [ObservableProperty]
        private int adjustMinutes;

        [ObservableProperty]
        private Selections selections = LoadSelections();

        [ObservableProperty]
        private bool hasScore;
        
        //private bool DifInit => CheckInitProcess();

        // =====================================================================
        // Display Properties (for View - null/empty/0 handling)
        // =====================================================================

        //public string DisplayPublishDate => publishDate?.ToString("dd-MM-yyyy") ?? "--";
        public string DisplayPublishDate => publishDate?.ToString("MMM dd, yyyy", System.Globalization.CultureInfo.InvariantCulture) ?? "--";
        public string DisplayPublisher => string.IsNullOrWhiteSpace(publisher) ? "--" : publisher;

        public string DisplayDeveloper => string.IsNullOrWhiteSpace(developer) ? "--" : developer;

        public string DisplaySeries => string.IsNullOrWhiteSpace(series) ? "--" : series;

        public string DisplayGenre => string.IsNullOrWhiteSpace(genre) ? "--" : genre;

        public string DisplayTheme => string.IsNullOrWhiteSpace(theme) ? "--" : theme;

        public string DisplayMetaScore => metaScore > 0 ? metaScore.ToString("F0") : "--";

        public string DisplayExcited => Game.Excited > 0 ? Game.Excited.ToString("F0") : "--";

        public string DisplayAvgPlayTime => avgPlaytime > 0 ? avgPlaytime.ToString() + "h" : "--";

        public string DisplayFinishedIn => string.IsNullOrWhiteSpace(finishedIn) ? "Not Finished" : finishedIn;

        public string DisplayProcessName => string.IsNullOrWhiteSpace(Game.ExeName) ? "--" : $"{Game.ExeName}.exe";

        public string DisplaySaveFolderPath => string.IsNullOrWhiteSpace(Game.SaveFolderPath) ? "--" : Game.SaveFolderPath;

        public string DisplayBackupFolderPath => string.IsNullOrWhiteSpace(Game.BackupFolderPath) ? "--" : Game.BackupFolderPath;

        public string DisplayOwnership => string.IsNullOrWhiteSpace(Game.Ownership) ? "--" : Game.Ownership;

        public string DisplayProgress => string.IsNullOrWhiteSpace(Game.Progress) ? "--" : Game.Progress;

        public string DisplayScore => Game.Score > 0 ? Game.Score.ToString("F1") : "--";

        public string DisplayPlayTime
        {
            get
            {
                if (Game.TotalPlayTimeSeconds <= 0) return "--";
                var ts = TimeSpan.FromSeconds(Game.TotalPlayTimeSeconds);
                return $"{(int)ts.TotalHours}h{ts.Minutes:D2}m";
            }
        }

        // =====================================================================
        // Commands - Mode Toggling & CRUD
        // =====================================================================

        [RelayCommand]
        private void ToggleEditMode()
        {
            IsReadOnly = !IsReadOnly;
            ReloadGameFromConfig();
            if (IsReadOnly) StatusMessage = string.Empty;
        }

        [RelayCommand]
        private async Task Sync()
        {
            if (string.IsNullOrWhiteSpace(Game.Name))
            {
                StatusMessage = "Game name is empty. Please enter a name first.";
                return;
            }

            IsBusy = true;
            StatusMessage = "Syncing...";

            try
            {
                var rawgGame = await ApiService.GetMetadata_Rawg(Game.Name);

                if (rawgGame != null)
                {
                    // ── Core updates ───────────────────────────────────────────────
                    if (!string.IsNullOrWhiteSpace(rawgGame.Name))
                        Game.Name = rawgGame.Name;

                    if (!string.IsNullOrWhiteSpace(rawgGame.Background_Image))
                    {
                        Game.ImagePath = rawgGame.Background_Image;
                        PreviewImage = ViewModelHelpers.LoadPreviewImage(Game.ImagePath);
                        OnPropertyChanged(nameof(PreviewImage));
                    }

                    // ── Update ViewModel wrapper properties ────────────────────────
                    Publisher = rawgGame.Publishers ?? string.Empty;
                    Developer = rawgGame.Developers ?? string.Empty;
                    Genre = rawgGame.Genres ?? string.Empty;
                 

                    // ── Update Game model properties (để binding trong Edit mode hoạt động) ──
                    Game.Publisher = Publisher;
                    Game.Developer = Developer;
                    Game.Genre = Genre;
                    Game.Series = Series;
                    Game.Theme = Theme;

                    metaScore = rawgGame.MetaScore;
                    avgPlaytime = rawgGame.AvgPlaytime;

                    if (rawgGame.ReleaseDate != null)
                    {
                        PublishDate = ApiService.ParseDate(rawgGame.ReleaseDate);
                    }
                    else
                    {
                        PublishDate = null;
                    }
                    Game.PublishDate = PublishDate;
                    Game.MetaScore = metaScore;
                    // Game.AvgPlayTime = avgPlaytime;   // nếu bạn dùng field này

                    // ── Force UI refresh cho tất cả các trường liên quan ───────────
                    OnPropertyChanged(nameof(Game));                    // quan trọng nhất
                    OnPropertyChanged(nameof(Game.Name));
                    OnPropertyChanged(nameof(Game.Publisher));
                    OnPropertyChanged(nameof(Game.Developer));
                    OnPropertyChanged(nameof(Game.Genre));
                    OnPropertyChanged(nameof(Game.Series));
                    OnPropertyChanged(nameof(Game.Theme));
                    OnPropertyChanged(nameof(Game.PublishDate));
                    OnPropertyChanged(nameof(Game.MetaScore));
                    // OnPropertyChanged(nameof(Game.AvgPlayTime));     // nếu cần

                    // Refresh các Display properties
                    OnPropertyChanged(nameof(DisplayPublisher));
                    OnPropertyChanged(nameof(DisplayDeveloper));
                    OnPropertyChanged(nameof(DisplaySeries));
                    OnPropertyChanged(nameof(DisplayGenre));
                    OnPropertyChanged(nameof(DisplayTheme));
                    OnPropertyChanged(nameof(DisplayMetaScore));
                    OnPropertyChanged(nameof(DisplayAvgPlayTime));
                    OnPropertyChanged(nameof(DisplayPublishDate));

                    StatusMessage = "Sync Successful!";
                }
                else
                {
                    StatusMessage = $"Cannot find game info for '{Game.Name}'";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Sync Error: {ex.Message}";
                Console.WriteLine($"Sync error: {ex}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void Save()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(Game.Name))
                {
                    UpdateBackupFolderPath(Game.Name);
                }

                var games = DataService.LoadGames();
                int index = games.FindIndex(g => g.Id == Game.Id);
                if (index >= 0)
                {
                    if (!string.IsNullOrWhiteSpace(Game.BackupFolderPath))
                    {
                        if (!Directory.Exists(Game.BackupFolderPath))
                        {
                            Directory.CreateDirectory(Game.BackupFolderPath);
                        }
                        string infoFilePath = Path.Combine(Game.BackupFolderPath, "save_location.txt");
                        File.WriteAllText(infoFilePath, Game.SaveFolderPath);
                    }

                    Game = ViewModelHelpers.InputFormatter(Game);
                    games[index] = Game;
                    DataService.SaveGamesData(games);

                    ReloadGameFromConfig();
                    StatusMessage = "Data Saved Successfully!";
                    IsReadOnly = true;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show(StatusMessage, "System Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void FinishGame()
        {
            var inputWindow = new InputScoreWindow(Game.Name);
            Game.Progress = "Done";
            Game.FinishedIn = DateTime.Now.Year.ToString();

            if (inputWindow.ShowDialog() == true)
            {
                if (int.TryParse(inputWindow.ScoreInput, out int newScore))
                {
                    Game.Score = newScore;
                    HasScore = newScore > 0;
                    OnPropertyChanged(nameof(DisplayScore));
                    StatusMessage = $"{Game.Name} has been finished with Score: {newScore}!";
                }
                else
                {
                    StatusMessage = "Invalid score input!";
                }
            }
            Save();
        }

        [RelayCommand]
        private void CheckPlayTime()
        {
            if (string.IsNullOrWhiteSpace(Game.Name)) return;

            string url = "https://howlongtobeat.com/?q=" + Uri.EscapeDataString(game.Name);

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true 
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Không thể mở trình duyệt: {ex.Message}");
            }
        }
        [RelayCommand]
        private void DeleteGame()
        {
            var result = MessageBox.Show($"Are you sure to Disable '{Game.Name}'?\n(Game will be hidden but data remains.)",
                "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var games = DataService.LoadGames();
                    var gameToDisable = games.FirstOrDefault(g => g.Id == Game.Id);
                    if (gameToDisable != null)
                    {
                        gameToDisable.IsDisabled = true;
                        DataService.SaveGamesData(games);
                        StatusMessage = $"{gameToDisable.Name} is Disabled!";
                        RaiseRequestClose(true);
                    }
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error: {ex.Message}";
                    MessageBox.Show(StatusMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            var games = DataService.LoadGames();
            var originalGame = games.FirstOrDefault(g => g.Id == Game.Id);
            if (originalGame != null)
            {
                Game = originalGame;
                BackupFolderPath = Game.BackupFolderPath;
                PreviewImage = ViewModelHelpers.LoadPreviewImage(Game.ImagePath);
                ReloadGameFromConfig();
            }
            IsReadOnly = true;
            StatusMessage = string.Empty;
        }
        [RelayCommand]
        private void EditSaveFolder()
        {
            var d = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Choose Save Folder",
                InitialDirectory = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments)
            };

            if (d.ShowDialog() == true)
            {
                Game.SaveFolderPath = d.FolderName;
            }
        }
        [RelayCommand]
        private void EditBackupFolder()
        {
            var d = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Choose Backup Folder",
                InitialDirectory = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments)
            };

            if (d.ShowDialog() == true)
            {
                // Trả về đường dẫn thư mục
                Game.BackupFolderPath = d.FolderName;
            }
        }


        [RelayCommand]
        private void Close() => RaiseRequestClose(false);

        // =====================================================================
        // Commands - Folder & Sync Helpers
        // =====================================================================

        [RelayCommand]
        private void OpenSaveFolder() => HandleFolderOpening((Game.SaveFolderPath, "Save Folder"));

        [RelayCommand]
        private void OpenBackupFolder() => HandleFolderOpening((Game.BackupFolderPath, "Backup Folder"));

        [RelayCommand]
        private void SyncImage()
        {
            if (string.IsNullOrWhiteSpace(Game.ImagePath))
            {
                PreviewImage = null;
                StatusMessage = "No image URL provided.";
                return;
            }

            try
            {
                PreviewImage = ViewModelHelpers.LoadPreviewImage(Game.ImagePath);
                StatusMessage = "Image preview updated successfully.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to load image: {ex.Message}";
            }
        }

        [RelayCommand]
        private void AdjustPlayTimeUp()
        {
            if (AdjustMinutes <= 0)
            {
                StatusMessage = "Please enter a positive number of minutes.";
                return;
            }

            Game.TotalPlayTimeSeconds += AdjustMinutes * 60L;
            UpdatePlayTimeFieldsFromSeconds();
            StatusMessage = $"Added {AdjustMinutes} minutes to play time.";
            AdjustMinutes = 0;
        }

        [RelayCommand]
        private void AdjustPlayTimeDown()
        {
            if (AdjustMinutes <= 0)
            {
                StatusMessage = "Please enter a positive number of minutes.";
                return;
            }

            long newSeconds = Game.TotalPlayTimeSeconds - (AdjustMinutes * 60L);
            if (newSeconds < 0)
            {
                Game.TotalPlayTimeSeconds = 0;
                StatusMessage = "Play time cannot be negative. Reset to 0.";
            }
            else
            {
                Game.TotalPlayTimeSeconds = newSeconds;
                StatusMessage = $"Subtracted {AdjustMinutes} minutes from play time.";
            }

            UpdatePlayTimeFieldsFromSeconds();
            AdjustMinutes = 0;
        }

        // =====================================================================
        // Private Helpers
        // =====================================================================

        private bool CheckInitProcess()
        {
            return string.Equals(Game.ExeName.ToLower(), Game.InitExeName.ToLower());
        }
        private void HandleFolderOpening(params (string? Path, string Desc)[] locations)
        {
            if (!FileHelper.TryOpenLocations(out var errors, locations))
            {
                MessageBox.Show(
                    "Cannot Open Folder:\n" + string.Join("\n", errors),
                    "Access Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void ReloadGameFromConfig()
        {
            var games = DataService.LoadGames();
            var reloaded = games.FirstOrDefault(g => g.Id == Game.Id);
            if (reloaded != null)
            {
                Game = reloaded;
                BackupFolderPath = Game.BackupFolderPath;
                PreviewImage = ViewModelHelpers.LoadPreviewImage(Game.ImagePath);

                if (IsReadOnly)
                {
                    SaveFolderAccessible = FileHelper.FileCheck(Game.SaveFolderPath);
                    BackupFolderAccessible = FileHelper.FileCheck(Game.BackupFolderPath);
                }
                else
                {
                    SaveFolderAccessible = true;
                    BackupFolderAccessible = true;
                    UpdatePlayTimeFieldsFromSeconds();
                }
            }
        }

        private void UpdateBackupFolderPath(string newGameName)
        {
            if (string.IsNullOrWhiteSpace(newGameName)) return;

            string backupRoot = DataService.GetBackupRootPath();
            string validFolderName = ViewModelHelpers.ValidFolderName(newGameName);
            string newBackupFolder = Path.Combine(backupRoot, validFolderName);

            if (!string.IsNullOrWhiteSpace(Game.BackupFolderPath) &&
                Directory.Exists(Game.BackupFolderPath) &&
                Game.BackupFolderPath != newBackupFolder)
            {
                try
                {
                    Directory.Move(Game.BackupFolderPath, newBackupFolder);
                    StatusMessage = $"Backup folder renamed to: {validFolderName}";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Failed to rename backup folder: {ex.Message}";
                }
            }
            else if (!Directory.Exists(newBackupFolder))
            {
                Directory.CreateDirectory(newBackupFolder);
            }

            Game.BackupFolderPath = newBackupFolder;
        }

        private void UpdatePlayTimeFieldsFromSeconds()
        {
            var ts = TimeSpan.FromSeconds(Game.TotalPlayTimeSeconds);
            PlayTimeHours = (int)ts.TotalHours;
            PlayTimeMinutes = ts.Minutes;
            OnPropertyChanged(nameof(PlayTimeHours));
            OnPropertyChanged(nameof(PlayTimeMinutes));
            OnPropertyChanged(nameof(DisplayPlayTime));
        }

        private void UpdateTotalPlayTimeFromFields()
        {
            Game.TotalPlayTimeSeconds = (PlayTimeHours * 3600L) + (PlayTimeMinutes * 60L);
            OnPropertyChanged(nameof(DisplayPlayTime));
        }

        // =====================================================================
        // Partial Property Changed Handlers
        // =====================================================================

        partial void OnGameChanged(Game? value)
        {
            PreviewImage = ViewModelHelpers.LoadPreviewImage(value?.ImagePath);

            if (!IsReadOnly && !string.IsNullOrWhiteSpace(value?.Name))
            {
                UpdateBackupFolderPath(value.Name);
            }

            if (value == null)
            {
                publisher = developer = series = genre = theme = finishedIn = string.Empty;
                publishDate = null;
                metaScore = 0;
                excited = 0f;
                avgPlaytime = 0L;
                HasScore = false;
            }
            else
            {
                publisher = value.Publisher ?? string.Empty;
                developer = value.Developer ?? string.Empty;
                series = value.Series ?? string.Empty;
                genre = value.Genre ?? string.Empty;
                theme = value.Theme ?? string.Empty;
                excited = value.Excited;
                finishedIn = value.FinishedIn ?? string.Empty;
                publishDate = value.PublishDate;
                metaScore = value.MetaScore;
                avgPlaytime = value.AvgPlayTime;

                HasScore = value.Score > 0f;
            }

            OnPropertyChanged(nameof(DisplayPublisher));
            OnPropertyChanged(nameof(DisplayDeveloper));
            OnPropertyChanged(nameof(DisplaySeries));
            OnPropertyChanged(nameof(DisplayGenre));
            OnPropertyChanged(nameof(DisplayTheme));
            OnPropertyChanged(nameof(DisplayExcited));
            OnPropertyChanged(nameof(DisplayFinishedIn));
            OnPropertyChanged(nameof(DisplayPublishDate));
            OnPropertyChanged(nameof(DisplayMetaScore));
            OnPropertyChanged(nameof(DisplayAvgPlayTime));
            OnPropertyChanged(nameof(DisplayProcessName));
            OnPropertyChanged(nameof(DisplaySaveFolderPath));
            OnPropertyChanged(nameof(DisplayBackupFolderPath));
            OnPropertyChanged(nameof(DisplayOwnership));
            OnPropertyChanged(nameof(DisplayProgress));
            OnPropertyChanged(nameof(DisplayScore));
            OnPropertyChanged(nameof(DisplayPlayTime));
        }

        partial void OnIsReadOnlyChanged(bool value)
        {
            if (!value) // Edit mode
            {
                UpdatePlayTimeFieldsFromSeconds();
            }
        }

        partial void OnPlayTimeHoursChanged(int value)
        {
            if (value < 0) PlayTimeHours = 0;
            UpdateTotalPlayTimeFromFields();
        }

        partial void OnPlayTimeMinutesChanged(int value)
        {
            if (value < 0) PlayTimeMinutes = 0;
            if (value >= 60)
            {
                PlayTimeHours += value / 60;
                PlayTimeMinutes = value % 60;
            }
            UpdateTotalPlayTimeFromFields();
        }

        partial void OnPublisherChanged(string value)
        {
            Game.Publisher = value;
            OnPropertyChanged(nameof(DisplayPublisher));
        }

        partial void OnDeveloperChanged(string value)
        {
            Game.Developer = value;
            OnPropertyChanged(nameof(DisplayDeveloper));
        }

        partial void OnSeriesChanged(string value)
        {
            Game.Series = value;
            OnPropertyChanged(nameof(DisplaySeries));
        }

        partial void OnGenreChanged(string value)
        {
            Game.Genre = value;
            OnPropertyChanged(nameof(DisplayGenre));
        }

        partial void OnThemeChanged(string value)
        {
            Game.Theme = value;
            OnPropertyChanged(nameof(DisplayTheme));
        }

        partial void OnExcitedChanged(float value)
        {
            Game.Excited = value;
            OnPropertyChanged(nameof(DisplayExcited));
        }

        partial void OnFinishedInChanged(string value)
        {
            Game.FinishedIn = value;
            OnPropertyChanged(nameof(DisplayFinishedIn));
        }

        partial void OnPublishDateChanged(DateOnly? value)
        {
            Game.PublishDate = value;
            OnPropertyChanged(nameof(DisplayPublishDate));
        }

        partial void OnMetaScoreChanged(int value)
        {
            Game.MetaScore = value;
            OnPropertyChanged(nameof(DisplayMetaScore));
        }

        partial void OnAvgPlaytimeChanged(long value)
        {
            Game.AvgPlayTime = value;
            OnPropertyChanged(nameof(DisplayAvgPlayTime));
        }
   
    }
}