using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MasterPlay.Helpers;
using MasterPlay.Models;
using MasterPlay.Models.Models;
using MasterPlay.UI.ViewModels.Generral;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace MasterPlay.UI.ViewModels
{
    public partial class AddGameViewModel : BaseViewModel
    {
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
        private string saveFolderPath = string.Empty;

        [ObservableProperty]
        private string backupFolderPath = string.Empty;

        [ObservableProperty]
        private string imagePath = string.Empty;

        [ObservableProperty]
        private string gImagePath = string.Empty;

        [ObservableProperty]
        private BitmapImage? previewImage;

        [ObservableProperty]
        private string publishDateInput = string.Empty;

        [ObservableProperty]
        private string publisher = string.Empty;

        [ObservableProperty]
        private string developer = string.Empty;

        [ObservableProperty]
        private double? score;

        [ObservableProperty]
        private int playTimeHours;

        [ObservableProperty]
        private int playTimeMinutes;

        [ObservableProperty]
        private int adjustMinutes;

        [ObservableProperty]
        private int? avgPlaytime;

        [ObservableProperty]
        private int? metaScore;

        [ObservableProperty]
        private double? excited;

        [ObservableProperty]
        private string finishedIn = string.Empty;

        [ObservableProperty]
        private bool hasInitExeName;

        [ObservableProperty]
        private string statusMessage = string.Empty;

        [ObservableProperty]
        private bool isBusy;

        // ==================== COLLECTIONS FOR DROPDOWNS ====================
        [ObservableProperty]
        private ObservableCollection<Series> allSeries = new();

        [ObservableProperty]
        private ObservableCollection<Genre> allGenres = new();

        [ObservableProperty]
        private ObservableCollection<Theme> allThemes = new();

        [ObservableProperty]
        private ObservableCollection<OwnershipStatus> allOwnerships = new();

        [ObservableProperty]
        private ObservableCollection<ProgressStatus> allProgresses = new();

        // ==================== SELECTED ITEMS ====================
        [ObservableProperty]
        private Series? selectedSeries;

        [ObservableProperty]
        private ObservableCollection<Genre> selectedGenres = new();

        [ObservableProperty]
        private ObservableCollection<Theme> selectedThemes = new();

        [ObservableProperty]
        private OwnershipStatus? selectedOwnership;

        [ObservableProperty]
        private ProgressStatus? selectedProgress;

        // ==================== POPUP STATE ====================
        [ObservableProperty]
        private bool isSeriesPopupOpen;

        [ObservableProperty]
        private bool isGenresPopupOpen;

        [ObservableProperty]
        private bool isThemesPopupOpen;

        [ObservableProperty]
        private string seriesSearchText = string.Empty;

        [ObservableProperty]
        private string genresSearchText = string.Empty;

        [ObservableProperty]
        private string themesSearchText = string.Empty;

        [ObservableProperty]
        private ObservableCollection<Series> filteredSeries = new();

        [ObservableProperty]
        private ObservableCollection<Genre> filteredGenres = new();

        [ObservableProperty]
        private ObservableCollection<Theme> filteredThemes = new();

        // ==================== DISPLAY HELPERS ====================
        public string CreateNewSeriesText => string.IsNullOrWhiteSpace(SeriesSearchText) ? "" : $"Create \"{SeriesSearchText}\"";
        public string CreateNewGenreText => string.IsNullOrWhiteSpace(GenresSearchText) ? "" : $"Create \"{GenresSearchText}\"";
        public string CreateNewThemeText => string.IsNullOrWhiteSpace(ThemesSearchText) ? "" : $"Create \"{ThemesSearchText}\"";
        public bool HasSlugName => !string.IsNullOrWhiteSpace(SlugName);

        // ==================== CONSTRUCTOR ====================
        public AddGameViewModel()
        {
            LoadLists();

            // Set default values: Progress = Pending (Id=6), Ownership = Owned (Id=5) - adjust IDs as per your DB
            SelectedProgress = AllProgresses.FirstOrDefault(p => p.Id == 6);
            SelectedOwnership = AllOwnerships.FirstOrDefault(o => o.Id == 4);

            SelectedGenres.CollectionChanged += (s, e) => OnPropertyChanged(nameof(SelectedGenres));
            SelectedThemes.CollectionChanged += (s, e) => OnPropertyChanged(nameof(SelectedThemes));
        }

        private void LoadLists()
        {
            AllSeries = new ObservableCollection<Series>(DataService.LoadSeriesList());
            AllGenres = new ObservableCollection<Genre>(DataService.LoadGenresList());
            AllThemes = new ObservableCollection<Theme>(DataService.LoadThemesList());
            AllOwnerships = new ObservableCollection<OwnershipStatus>(DataService.LoadOwnershipList());
            AllProgresses = new ObservableCollection<ProgressStatus>(DataService.LoadProgressList());
        }

        // ==================== COMMANDS ====================

        [RelayCommand]
        private async Task Sync()
        {
            if (string.IsNullOrWhiteSpace(GameName))
            {
                await ShowStatus("Game name is empty. Please enter a name first.");
                return;
            }

            IsBusy = true;
            await ShowStatus("Syncing...", 0);

            try
            {
                var rawgGame = await RawgService.GetMetadata_Rawg(GameName);

                if (rawgGame != null)
                {
                    GameName = rawgGame.Name ?? GameName;
                    Publisher = rawgGame.Publishers ?? string.Empty;
                    Developer = rawgGame.Developers ?? string.Empty;
                    SlugName = rawgGame.SlugName ?? string.Empty;
                    MetaScore = rawgGame.MetaScore;
                    AvgPlaytime = rawgGame.AvgPlaytime;

                    if (!string.IsNullOrWhiteSpace(rawgGame.Background_Image))
                    {
                        ImagePath = rawgGame.Background_Image;
                        GImagePath = rawgGame.Background_Image;
                        PreviewImage = LoadPreviewImage(ImagePath);
                    }

                    var galleryImg = await RawgService.GetGalleryImage(GameName);
                    if (galleryImg != null)
                    {
                        GImagePath = galleryImg;
                    }

                    SyncPublishDate(ViewModelHelpers.StringToDateOnly(rawgGame.ReleaseDate));

                    await ShowStatus("Sync Successful!");
                }
                else
                {
                    await ShowStatus($"Cannot find game info for '{GameName}'");
                }
            }
            catch (Exception ex)
            {
                await ShowStatus($"Sync Error: {ex.Message}");
                Debug.WriteLine($"Sync error: {ex}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void SyncPublishDate(DateOnly? date)
        {
            PublishDateInput = date?.ToString("dd/MM/yyyy") ?? string.Empty;
        }

        [RelayCommand]
        private async Task SyncImage()
        {
            // Try to update preview from ImagePath first, if available
            if (!string.IsNullOrWhiteSpace(ImagePath))
            {
                try
                {
                    PreviewImage = LoadPreviewImage(ImagePath);
                    await ShowStatus("Image preview updated successfully.");
                }
                catch (Exception ex)
                {
                    await ShowStatus($"Failed to load image: {ex.Message}");
                }
            }
            else
            {
                await ShowStatus("No image URL provided.");
            }
        }

        [RelayCommand]
        private void AdjustPlayTimeUp()
        {
            if (AdjustMinutes <= 0)
            {
                ShowStatus("Please enter a positive number of minutes.");
                return;
            }
            int totalSeconds = (PlayTimeHours * 3600 + PlayTimeMinutes * 60) + AdjustMinutes * 60;
            PlayTimeHours = totalSeconds / 3600;
            PlayTimeMinutes = (totalSeconds % 3600) / 60;
            ShowStatus($"Added {AdjustMinutes} minutes to play time.");
            AdjustMinutes = 0;
        }

        [RelayCommand]
        private void AdjustPlayTimeDown()
        {
            if (AdjustMinutes <= 0)
            {
                ShowStatus("Please enter a positive number of minutes.");
                return;
            }
            int totalSeconds = (PlayTimeHours * 3600 + PlayTimeMinutes * 60) - AdjustMinutes * 60;
            if (totalSeconds < 0) totalSeconds = 0;
            PlayTimeHours = totalSeconds / 3600;
            PlayTimeMinutes = (totalSeconds % 3600) / 60;
            ShowStatus($"Subtracted {AdjustMinutes} minutes from play time.");
            AdjustMinutes = 0;
        }

        [RelayCommand]
        private void CheckPlayTime()
        {
            if (string.IsNullOrWhiteSpace(GameName)) return;
            string url = "https://howlongtobeat.com/?q=" + Uri.EscapeDataString(GameName);
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch (Exception ex) { Debug.WriteLine($"Cannot open browser: {ex.Message}"); }
        }

        [RelayCommand]
        private void CheckMetaScore()
        {
            if (string.IsNullOrWhiteSpace(SlugName)) return;
            string url = "https://www.metacritic.com/game/" + Uri.EscapeDataString(SlugName) + "/";
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch (Exception ex) { Debug.WriteLine($"Cannot open browser: {ex.Message}"); }
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
                SaveFolderPath = dialog.FolderName;
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
                BackupFolderPath = dialog.FolderName;
        }

        [RelayCommand]
        private async Task AddGame()
        {
            if (string.IsNullOrWhiteSpace(GameName))
            {
                await ShowStatus("Game Name is required.");
                return;
            }
            if (string.IsNullOrWhiteSpace(ImagePath))
            {
                await ShowStatus("Cover Image URL is required.");
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


                // Prepare Game object
                var newGame = new Game
                {
                    Name = GameName.Trim(),
                    SlugName = SlugName?.Trim(),
                    ExeName = ExeName?.Trim(),
                    InitExeName = HasInitExeName ? InitExeName?.Trim() : ExeName?.Trim(),
                    SaveFolderPath = SaveFolderPath?.Trim(),
                    ImagePath = ImagePath?.Trim(),
                    GImagePath = GImagePath?.Trim(),
                    PublishDate = ViewModelHelpers.StringToDateOnly(PublishDateInput),
                    Publisher = Publisher?.Trim(),
                    Developer = Developer?.Trim(),
                    SeriesId = SelectedSeries?.Id,
                    Score = Score,
                    TotalPlayTimeSeconds = PlayTimeHours * 3600 + PlayTimeMinutes * 60,
                    MetaScore = MetaScore,
                    AvgPlayTime = AvgPlaytime,
                    Excited = Excited,
                    OwnershipId = SelectedOwnership?.Id,
                    ProgressId = SelectedProgress?.Id,
                    FinishedIn = FinishedIn?.Trim(),
                    IsDisabled = false
                };

                // Handle GameGenres
                newGame.GameGenres = new List<GameGenre>();
                foreach (var genre in SelectedGenres)
                {
                    newGame.GameGenres.Add(new GameGenre { GameId = newGame.Id, GenreId = genre.Id });
                }

                // Handle GameThemes
                newGame.GameThemes = new List<GameTheme>();
                foreach (var theme in SelectedThemes)
                {
                    newGame.GameThemes.Add(new GameTheme { GameId = newGame.Id, ThemeId = theme.Id });
                }

                newGame = ViewModelHelpers.InputFormatter(newGame);

                DataService.AddGame(newGame);

                RaiseRequestClose(true);
            }
            catch (Exception ex)
            {
                await ShowStatus($"Error: {ex.Message}");
            }
        }

        [RelayCommand]
        private void Cancel() => RaiseRequestClose(false);

        // ==================== CHIP COMMANDS ====================

        [RelayCommand]
        private void OpenSeriesPopup()
        {
            SeriesSearchText = string.Empty;
            FilteredSeries = new ObservableCollection<Series>(AllSeries);
            IsSeriesPopupOpen = true;
        }

        [RelayCommand]
        private void SelectSeries(Series series)
        {
            SelectedSeries = series;
            IsSeriesPopupOpen = false;
        }

        [RelayCommand]
        private void CreateNewSeries()
        {
            if (!string.IsNullOrWhiteSpace(SeriesSearchText))
            {
                var series = DataService.AddSeries(SeriesSearchText);
                if (!AllSeries.Any(s => s.Id == series.Id))
                    AllSeries.Add(series);
                SelectedSeries = series;
            }
            IsSeriesPopupOpen = false;
        }

        [RelayCommand]
        private void RemoveSelectedSeries()
        {
            SelectedSeries = null;
        }

        [RelayCommand]
        private void OpenGenresPopup()
        {
            GenresSearchText = string.Empty;
            FilteredGenres = new ObservableCollection<Genre>(AllGenres);
            IsGenresPopupOpen = true;
        }

        [RelayCommand]
        private void SelectGenre(Genre genre)
        {
            if (!SelectedGenres.Any(g => g.Id == genre.Id))
                SelectedGenres.Add(genre);
        }

        [RelayCommand]
        private void CreateNewGenre()
        {
            if (!string.IsNullOrWhiteSpace(GenresSearchText))
            {
                var genre = DataService.AddGenre(GenresSearchText);
                if (!AllGenres.Any(g => g.Id == genre.Id))
                    AllGenres.Add(genre);
                if (!SelectedGenres.Any(g => g.Id == genre.Id))
                    SelectedGenres.Add(genre);
            }
        }

        [RelayCommand]
        private void RemoveGenre(Genre genre)
        {
            if (SelectedGenres.Contains(genre))
                SelectedGenres.Remove(genre);
        }

        [RelayCommand]
        private void OpenThemesPopup()
        {
            ThemesSearchText = string.Empty;
            FilteredThemes = new ObservableCollection<Theme>(AllThemes);
            IsThemesPopupOpen = true;
        }

        [RelayCommand]
        private void SelectTheme(Theme theme)
        {
            if (!SelectedThemes.Any(t => t.Id == theme.Id))
                SelectedThemes.Add(theme);
        }

        [RelayCommand]
        private void CreateNewTheme()
        {
            if (!string.IsNullOrWhiteSpace(ThemesSearchText))
            {
                var theme = DataService.AddTheme(ThemesSearchText);
                if (!AllThemes.Any(t => t.Id == theme.Id))
                    AllThemes.Add(theme);
                if (!SelectedThemes.Any(t => t.Id == theme.Id))
                    SelectedThemes.Add(theme);
            }
        }

        [RelayCommand]
        private void RemoveTheme(Theme theme)
        {
            if (SelectedThemes.Contains(theme))
                SelectedThemes.Remove(theme);
        }

        // ==================== PRIVATE HELPERS ====================

        private async Task ShowStatus(string message, int delayMs = 3000)
        {
            _msgCts?.Cancel();
            _msgCts = new CancellationTokenSource();
            var token = _msgCts.Token;

            StatusMessage = message;

            if (delayMs > 0)
            {
                try
                {
                    await Task.Delay(delayMs, token);
                    if (!token.IsCancellationRequested)
                        StatusMessage = string.Empty;
                }
                catch (TaskCanceledException) { }
            }
        }

        private BitmapImage? LoadPreviewImage(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            try { return new BitmapImage(new Uri(path)); }
            catch { return null; }
        }

        // ==================== PARTIAL METHODS ====================

        partial void OnImagePathChanged(string value)
        {
            PreviewImage = LoadPreviewImage(value);
        }

        partial void OnGImagePathChanged(string value)
        {
            // Không cần cập nhật preview, gallery dùng converter riêng
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

        partial void OnSeriesSearchTextChanged(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                FilteredSeries = new ObservableCollection<Series>(AllSeries);
            else
                FilteredSeries = new ObservableCollection<Series>(
                    AllSeries.Where(x => x.Name.Contains(value, StringComparison.OrdinalIgnoreCase)));
            OnPropertyChanged(nameof(CreateNewSeriesText));
        }

        partial void OnGenresSearchTextChanged(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                FilteredGenres = new ObservableCollection<Genre>(AllGenres);
            else
                FilteredGenres = new ObservableCollection<Genre>(
                    AllGenres.Where(x => x.Name.Contains(value, StringComparison.OrdinalIgnoreCase)));
            OnPropertyChanged(nameof(CreateNewGenreText));
        }

        partial void OnThemesSearchTextChanged(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                FilteredThemes = new ObservableCollection<Theme>(AllThemes);
            else
                FilteredThemes = new ObservableCollection<Theme>(
                    AllThemes.Where(x => x.Name.Contains(value, StringComparison.OrdinalIgnoreCase)));
            OnPropertyChanged(nameof(CreateNewThemeText));
        }

        partial void OnInitExeNameChanged(string value)
        {
            // No extra logic needed
        }

        partial void OnHasInitExeNameChanged(bool value)
        {
            if (!value) InitExeName = string.Empty;
        }
    }
}