using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MasterPlay.Helpers;
using MasterPlay.Models;
using MasterPlay.Models.Models;
using MasterPlay.UI.ViewModels.Generral;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace MasterPlay.UI.ViewModels
{
    public partial class AddGameViewModel : BaseViewModel
    {
        private CancellationTokenSource? _msgCts;

        // ==================== BASIC INFO ====================
        [ObservableProperty]
        private string _gameName = string.Empty;

        [ObservableProperty]
        private string _slugName = string.Empty;

        [ObservableProperty]
        private string _exeName = string.Empty;

        [ObservableProperty]
        private string _initExeName = string.Empty;

        [ObservableProperty]
        private string _saveFolderPath = string.Empty;

        [ObservableProperty]
        private string _backupFolderPath = string.Empty;

        [ObservableProperty]
        private string _imagePath = string.Empty;

        [ObservableProperty]
        private string _gImagePath = string.Empty;

        [ObservableProperty]
        private BitmapImage? _previewImage;

        [ObservableProperty]
        private string _publishDateInput = string.Empty;

        [ObservableProperty]
        private string _publisher = string.Empty;

        [ObservableProperty]
        private string _developer = string.Empty;

        [ObservableProperty]
        private double? _score;

        [ObservableProperty]
        private int _playTimeHours;

        [ObservableProperty]
        private int _playTimeMinutes;

        [ObservableProperty]
        private int _adjustMinutes;

        [ObservableProperty]
        private int? _avgPlaytime;

        [ObservableProperty]
        private int? _metaScore;

        [ObservableProperty]
        private double? _excited;

        [ObservableProperty]
        private string _finishedIn = string.Empty;

        [ObservableProperty]
        private bool _hasInitExeName;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private bool _isBusy;

        // ==================== COLLECTIONS ====================
        [ObservableProperty]
        private ObservableCollection<Series> _allSeries = new();

        [ObservableProperty]
        private ObservableCollection<Genre> _allGenres = new();

        [ObservableProperty]
        private ObservableCollection<Theme> _allThemes = new();

        [ObservableProperty]
        private ObservableCollection<OwnershipStatus> _allOwnerships = new();

        [ObservableProperty]
        private ObservableCollection<ProgressStatus> _allProgresses = new();

        // ==================== SELECTED ITEMS ====================
        [ObservableProperty]
        private Series? _selectedSeries;

        [ObservableProperty]
        private ObservableCollection<Genre> _selectedGenres = new();

        [ObservableProperty]
        private ObservableCollection<Theme> _selectedThemes = new();

        [ObservableProperty]
        private OwnershipStatus? _selectedOwnership;

        [ObservableProperty]
        private ProgressStatus? _selectedProgress;

        // ==================== POPUP STATE ====================
        [ObservableProperty]
        private bool _isSeriesPopupOpen;

        [ObservableProperty]
        private bool _isGenresPopupOpen;

        [ObservableProperty]
        private bool _isThemesPopupOpen;

        [ObservableProperty]
        private string _seriesSearchText = string.Empty;

        [ObservableProperty]
        private string _genresSearchText = string.Empty;

        [ObservableProperty]
        private string _themesSearchText = string.Empty;

        [ObservableProperty]
        private ObservableCollection<Series> _filteredSeries = new();

        [ObservableProperty]
        private ObservableCollection<Genre> _filteredGenres = new();

        [ObservableProperty]
        private ObservableCollection<Theme> _filteredThemes = new();

        // ==================== EDIT POPUP STATE ====================
        [ObservableProperty]
        private bool _isEditSeriesPopupOpen;

        [ObservableProperty]
        private bool _isEditGenrePopupOpen;

        [ObservableProperty]
        private bool _isEditThemePopupOpen;

        [ObservableProperty]
        private Series? _editingSeries;

        [ObservableProperty]
        private Genre? _editingGenre;

        [ObservableProperty]
        private Theme? _editingTheme;

        [ObservableProperty]
        private string _editSeriesName = string.Empty;

        [ObservableProperty]
        private string _editGenreName = string.Empty;

        [ObservableProperty]
        private string _editThemeName = string.Empty;

        // ==================== DISPLAY HELPERS ====================
        public string CreateNewSeriesText => string.IsNullOrWhiteSpace(SeriesSearchText) ? "" : $"Create \"{SeriesSearchText}\"";
        public string CreateNewGenreText => string.IsNullOrWhiteSpace(GenresSearchText) ? "" : $"Create \"{GenresSearchText}\"";
        public string CreateNewThemeText => string.IsNullOrWhiteSpace(ThemesSearchText) ? "" : $"Create \"{ThemesSearchText}\"";
        public bool HasSlugName => !string.IsNullOrWhiteSpace(SlugName);

        // ==================== CONSTRUCTOR ====================
        public AddGameViewModel()
        {
            LoadLists();

            // Default values: Progress = Pending (Id=6), Ownership = Not Owned (Id=4) - adjust as needed
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

            FilteredSeries = new ObservableCollection<Series>(AllSeries);
            FilteredGenres = new ObservableCollection<Genre>(AllGenres);
            FilteredThemes = new ObservableCollection<Theme>(AllThemes);
        }

        // ==================== FILTER METHODS ====================
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

        // ==================== MAIN COMMANDS ====================
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

                newGame.GameGenres = new List<GameGenre>();
                foreach (var genre in SelectedGenres)
                {
                    newGame.GameGenres.Add(new GameGenre { GameId = newGame.Id, GenreId = genre.Id });
                }

                newGame.GameThemes = new List<GameTheme>();
                foreach (var theme in SelectedThemes)
                {
                    newGame.GameThemes.Add(new GameTheme { GameId = newGame.Id, ThemeId = theme.Id });
                }

                newGame = ViewModelHelpers.InputFormatter(newGame);

                // ==================== UPLOAD ANH LEN CLOUDINARY ====================
                // Moi game chi luu dung 1 anh moi nhat cho moi loai (cover / gallery),
                // vi PublicId co dinh theo GameId va Overwrite = true.
                try
                {
                    IsBusy = true;

                    if (!string.IsNullOrWhiteSpace(newGame.ImagePath))
                    {
                        await ShowStatus("Dang tai anh cover len Cloudinary...", 0);
                        var coverUrl = await CloudinaryImageService.UploadGameImageAsync(newGame.Id, newGame.ImagePath, "cover");
                        if (!string.IsNullOrWhiteSpace(coverUrl))
                            newGame.ImagePath = coverUrl;
                    }

                    if (!string.IsNullOrWhiteSpace(newGame.GImagePath))
                    {
                        await ShowStatus("Dang tai anh gallery len Cloudinary...", 0);
                        var galleryUrl = await CloudinaryImageService.UploadGameImageAsync(newGame.Id, newGame.GImagePath, "gallery");
                        if (!string.IsNullOrWhiteSpace(galleryUrl))
                            newGame.GImagePath = galleryUrl;
                    }
                }
                catch (Exception cloudEx)
                {
                    // Khong chan viec them game neu Cloudinary loi - giu lai link anh goc
                    Debug.WriteLine($"Cloudinary upload error: {cloudEx.Message}");
                }
                finally
                {
                    IsBusy = false;
                }
                // ======================================================================

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

        // ==================== POPUP OPEN COMMANDS ====================
        [RelayCommand]
        private void OpenSeriesPopup()
        {
            SeriesSearchText = string.Empty;
            FilteredSeries = new ObservableCollection<Series>(AllSeries);
            IsSeriesPopupOpen = true;
        }

        [RelayCommand]
        private void OpenGenresPopup()
        {
            GenresSearchText = string.Empty;
            FilteredGenres = new ObservableCollection<Genre>(AllGenres);
            IsGenresPopupOpen = true;
        }

        [RelayCommand]
        private void OpenThemesPopup()
        {
            ThemesSearchText = string.Empty;
            FilteredThemes = new ObservableCollection<Theme>(AllThemes);
            IsThemesPopupOpen = true;
        }

        // ==================== SELECTION COMMANDS ====================
        [RelayCommand]
        private void SelectSeries(Series series)
        {
            SelectedSeries = series;
            IsSeriesPopupOpen = false;
        }

        [RelayCommand]
        private void SelectGenre(Genre genre)
        {
            if (!SelectedGenres.Any(g => g.Id == genre.Id))
                SelectedGenres.Add(genre);
        }

        [RelayCommand]
        private void SelectTheme(Theme theme)
        {
            if (!SelectedThemes.Any(t => t.Id == theme.Id))
                SelectedThemes.Add(theme);
        }

        // ==================== CREATE NEW COMMANDS ====================
        [RelayCommand]
        private void CreateNewSeries()
        {
            if (!string.IsNullOrWhiteSpace(SeriesSearchText))
            {
                var series = DataService.AddSeries(SeriesSearchText);
                if (!AllSeries.Any(s => s.Id == series.Id))
                {
                    AllSeries.Add(series);
                    AllSeries = new ObservableCollection<Series>(AllSeries.OrderBy(s => s.Name));
                    FilteredSeries = new ObservableCollection<Series>(AllSeries);
                }
                SelectedSeries = series;
                IsSeriesPopupOpen = false;
            }
        }

        [RelayCommand]
        private void CreateNewGenre()
        {
            if (!string.IsNullOrWhiteSpace(GenresSearchText))
            {
                var genre = DataService.AddGenre(GenresSearchText);
                if (!AllGenres.Any(g => g.Id == genre.Id))
                {
                    AllGenres.Add(genre);
                    AllGenres = new ObservableCollection<Genre>(AllGenres.OrderBy(g => g.Name));
                    FilteredGenres = new ObservableCollection<Genre>(AllGenres);
                }
                if (!SelectedGenres.Any(g => g.Id == genre.Id))
                    SelectedGenres.Add(genre);
                IsGenresPopupOpen = false;
            }
        }

        [RelayCommand]
        private void CreateNewTheme()
        {
            if (!string.IsNullOrWhiteSpace(ThemesSearchText))
            {
                var theme = DataService.AddTheme(ThemesSearchText);
                if (!AllThemes.Any(t => t.Id == theme.Id))
                {
                    AllThemes.Add(theme);
                    AllThemes = new ObservableCollection<Theme>(AllThemes.OrderBy(t => t.Name));
                    FilteredThemes = new ObservableCollection<Theme>(AllThemes);
                }
                if (!SelectedThemes.Any(t => t.Id == theme.Id))
                    SelectedThemes.Add(theme);
                IsThemesPopupOpen = false;
            }
        }

        // ==================== REMOVE COMMANDS ====================
        [RelayCommand]
        private void RemoveSelectedSeries() => SelectedSeries = null;

        [RelayCommand]
        private void RemoveGenre(Genre genre)
        {
            if (SelectedGenres.Contains(genre))
                SelectedGenres.Remove(genre);
        }

        [RelayCommand]
        private void RemoveTheme(Theme theme)
        {
            if (SelectedThemes.Contains(theme))
                SelectedThemes.Remove(theme);
        }

        // ==================== EDIT COMMANDS ====================
        [RelayCommand]
        private void OpenEditSeriesPopup(Series series)
        {
            IsSeriesPopupOpen = false; // Dong popup chon Series
            EditingSeries = series;
            EditSeriesName = series.Name;
            IsEditSeriesPopupOpen = true;
        }

        [RelayCommand]
        private void OpenEditGenrePopup(Genre genre)
        {
            IsGenresPopupOpen = false; // Dong popup chon Genre
            EditingGenre = genre;
            EditGenreName = genre.Name;
            IsEditGenrePopupOpen = true;
        }

        [RelayCommand]
        private void OpenEditThemePopup(Theme theme)
        {
            IsThemesPopupOpen = false; // Dong popup chon Theme
            EditingTheme = theme;
            EditThemeName = theme.Name;
            IsEditThemePopupOpen = true;
        }

        [RelayCommand]
        private void SaveSeriesEdit()
        {
            if (EditingSeries == null) return;
            if (string.IsNullOrWhiteSpace(EditSeriesName))
            {
                MessageBox.Show("Series name cannot be empty.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var success = DataService.UpdateSeries(EditingSeries.Id, EditSeriesName.Trim());
            if (success)
            {
                EditingSeries.Name = EditSeriesName.Trim();
                // Refresh collections
                AllSeries = new ObservableCollection<Series>(AllSeries.OrderBy(s => s.Name));
                FilteredSeries = new ObservableCollection<Series>(AllSeries);
                OnPropertyChanged(nameof(AllSeries));
            }
            IsEditSeriesPopupOpen = false;
            EditingSeries = null;
        }

        [RelayCommand]
        private void SaveGenreEdit()
        {
            if (EditingGenre == null) return;
            if (string.IsNullOrWhiteSpace(EditGenreName))
            {
                MessageBox.Show("Genre name cannot be empty.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var success = DataService.UpdateGenre(EditingGenre.Id, EditGenreName.Trim());
            if (success)
            {
                EditingGenre.Name = EditGenreName.Trim();
                AllGenres = new ObservableCollection<Genre>(AllGenres.OrderBy(g => g.Name));
                FilteredGenres = new ObservableCollection<Genre>(AllGenres);
                OnPropertyChanged(nameof(AllGenres));
            }
            IsEditGenrePopupOpen = false;
            EditingGenre = null;
        }

        [RelayCommand]
        private void SaveThemeEdit()
        {
            if (EditingTheme == null) return;
            if (string.IsNullOrWhiteSpace(EditThemeName))
            {
                MessageBox.Show("Theme name cannot be empty.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var success = DataService.UpdateTheme(EditingTheme.Id, EditThemeName.Trim());
            if (success)
            {
                EditingTheme.Name = EditThemeName.Trim();
                AllThemes = new ObservableCollection<Theme>(AllThemes.OrderBy(t => t.Name));
                FilteredThemes = new ObservableCollection<Theme>(AllThemes);
                OnPropertyChanged(nameof(AllThemes));
            }
            IsEditThemePopupOpen = false;
            EditingTheme = null;
        }

        [RelayCommand]
        private void DeleteSeries()
        {
            if (EditingSeries == null) return;
            var result = MessageBox.Show($"Are you sure you want to delete '{EditingSeries.Name}'? This will remove all associations with games.",
                                         "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            var success = DataService.DeleteSeries(EditingSeries.Id);
            if (success)
            {
                if (SelectedSeries?.Id == EditingSeries.Id)
                    SelectedSeries = null;
                AllSeries.Remove(EditingSeries);
                AllSeries = new ObservableCollection<Series>(AllSeries.OrderBy(s => s.Name));
                FilteredSeries = new ObservableCollection<Series>(AllSeries);
                OnPropertyChanged(nameof(AllSeries));
            }
            IsEditSeriesPopupOpen = false;
            EditingSeries = null;
        }

        [RelayCommand]
        private void DeleteGenre()
        {
            if (EditingGenre == null) return;
            var result = MessageBox.Show($"Are you sure you want to delete '{EditingGenre.Name}'? This will remove all associations with games.",
                                         "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            var success = DataService.DeleteGenre(EditingGenre.Id);
            if (success)
            {
                if (SelectedGenres.Any(g => g.Id == EditingGenre.Id))
                    SelectedGenres.Remove(EditingGenre);
                AllGenres.Remove(EditingGenre);
                AllGenres = new ObservableCollection<Genre>(AllGenres.OrderBy(g => g.Name));
                FilteredGenres = new ObservableCollection<Genre>(AllGenres);
                OnPropertyChanged(nameof(AllGenres));
            }
            IsEditGenrePopupOpen = false;
            EditingGenre = null;
        }

        [RelayCommand]
        private void DeleteTheme()
        {
            if (EditingTheme == null) return;
            var result = MessageBox.Show($"Are you sure you want to delete '{EditingTheme.Name}'? This will remove all associations with games.",
                                         "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            var success = DataService.DeleteTheme(EditingTheme.Id);
            if (success)
            {
                if (SelectedThemes.Any(t => t.Id == EditingTheme.Id))
                    SelectedThemes.Remove(EditingTheme);
                AllThemes.Remove(EditingTheme);
                AllThemes = new ObservableCollection<Theme>(AllThemes.OrderBy(t => t.Name));
                FilteredThemes = new ObservableCollection<Theme>(AllThemes);
                OnPropertyChanged(nameof(AllThemes));
            }
            IsEditThemePopupOpen = false;
            EditingTheme = null;
        }

        [RelayCommand]
        private void CancelEditSeries() => IsEditSeriesPopupOpen = false;
        [RelayCommand]
        private void CancelEditGenre() => IsEditGenrePopupOpen = false;
        [RelayCommand]
        private void CancelEditTheme() => IsEditThemePopupOpen = false;

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

        // Partial methods
        partial void OnImagePathChanged(string value) => PreviewImage = LoadPreviewImage(value);
        partial void OnPlayTimeHoursChanged(int value) { if (value < 0) PlayTimeHours = 0; }
        partial void OnPlayTimeMinutesChanged(int value)
        {
            if (value < 0) PlayTimeMinutes = 0;
            if (value >= 60)
            {
                PlayTimeHours += value / 60;
                PlayTimeMinutes = value % 60;
            }
        }
        partial void OnHasInitExeNameChanged(bool value) { if (!value) InitExeName = string.Empty; }
    }
}