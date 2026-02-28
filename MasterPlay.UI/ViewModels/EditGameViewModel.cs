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
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace MasterPlay.UI.ViewModels
{
    public partial class EditGameViewModel : BaseViewModel
    {
        // ==================== FIELDS & CONSTRUCTOR ====================
        private static Selections LoadSelections() => DataService.LoadSelection();

        public EditGameViewModel() : this(new Game()) { }

        public EditGameViewModel(Game selectedGame)
        {
            Game = selectedGame ?? new Game();
            ReloadGameFromConfig();
            DataService.AssignDefaultColors();
            InitializeMockData();
            LoadGameData();
            hasInitExeName = !string.IsNullOrWhiteSpace(Game.InitExeName);
        }

        // ==================== CORE PROPERTIES ====================
        [ObservableProperty]
        private Game game;

        [ObservableProperty]
        private string publisher = string.Empty;
        [ObservableProperty]
        private string slugName = string.Empty;

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

        [ObservableProperty]
        private string publishDateInput = string.Empty;

        [ObservableProperty]
        private bool hasInitExeName;

        // ==================== CHIP PROPERTIES ====================
        [ObservableProperty]
        private ObservableCollection<string> allSeries;

        [ObservableProperty]
        private ObservableCollection<string> allGenres;

        [ObservableProperty]
        private ObservableCollection<string> allThemes;

        [ObservableProperty]
        private string? selectedSeries;

        [ObservableProperty]
        private ObservableCollection<string> selectedGenres;

        [ObservableProperty]
        private ObservableCollection<string> selectedThemes;

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
        private ObservableCollection<string> filteredSeries;

        [ObservableProperty]
        private ObservableCollection<string> filteredGenres;

        [ObservableProperty]
        private ObservableCollection<string> filteredThemes;

        public string CreateNewSeriesText => string.IsNullOrWhiteSpace(SeriesSearchText) ? "" : $"Create \"{SeriesSearchText}\"";
        public string CreateNewGenreText => string.IsNullOrWhiteSpace(GenresSearchText) ? "" : $"Create \"{GenresSearchText}\"";
        public string CreateNewThemeText => string.IsNullOrWhiteSpace(ThemesSearchText) ? "" : $"Create \"{ThemesSearchText}\"";

        // ==================== DISPLAY PROPERTIES ====================
        public string DisplayPublishDate => publishDate?.ToString("MMM dd, yyyy") ?? "--";
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
        public string DisplayInitProcessName => string.IsNullOrWhiteSpace(Game.InitExeName) ? "--" : $"{Game.InitExeName}.exe";
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

        public bool HasSlugName => !string.IsNullOrWhiteSpace(Game?.SlugName);
        public bool HasInitExeNameForView => !string.IsNullOrWhiteSpace(Game?.InitExeName);

        // ==================== MODE & CRUD COMMANDS ====================
        [RelayCommand]
        private void ToggleEditMode()
        {
            IsReadOnly = !IsReadOnly;
            if (!IsReadOnly)
                LoadGameData();
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
                        Game.GImagePath = rawgGame.Background_Image;
                        PreviewImage = ViewModelHelpers.LoadPreviewImage(Game.ImagePath);
                        OnPropertyChanged(nameof(PreviewImage));
                    }

                    // ── Update ViewModel wrapper properties ────────────────────────
                    Publisher = rawgGame.Publishers ?? string.Empty;
                    Developer = rawgGame.Developers ?? string.Empty;
                    Genre = rawgGame.Genres ?? string.Empty;
                    SyncPublishDate(ViewModelHelpers.StringToDateOnly(rawgGame.ReleaseDate));


                    // ── Update Game model properties (để binding trong Edit mode hoạt động) ──
                    Game.Publisher = Publisher;
                    Game.Developer = Developer;
                    //Game.Genre = Genre;
                    //Game.Series = Series;
                    Game.Theme = Theme;

                    slugName = rawgGame.SlugName;
                    metaScore = rawgGame.MetaScore;
                    avgPlaytime = rawgGame.AvgPlaytime;



                    var galleryImg = await ApiService.GetGalleryImage(Game.Name);
                    if (galleryImg != null)
                    {
                        Game.GImagePath = galleryImg;
                    }

                    PublishDate = ViewModelHelpers.StringToDateOnly(rawgGame.ReleaseDate);

                    Game.PublishDate = PublishDate;
                    Game.MetaScore = metaScore;
                    Game.SlugName = slugName;
                    // Game.AvgPlayTime = avgPlaytime; 

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
                    OnPropertyChanged(nameof(PublishDateInput));

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

        private void SyncPublishDate(DateOnly? date)
        {
            PublishDateInput = date?.ToString("dd/MM/yyyy") ?? string.Empty;
        }

        [RelayCommand]
        private void Save()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(Game.Name))
                    UpdateBackupFolderPath(Game.Name);

                Game.Series = SelectedSeries ?? string.Empty;
                Game.Genre = SelectedGenres != null ? string.Join(", ", SelectedGenres) : string.Empty;
                Game.Theme = SelectedThemes != null ? string.Join(", ", SelectedThemes) : string.Empty;

                Game.PublishDate = ViewModelHelpers.StringToDateOnly(publishDateInput);
                var games = DataService.LoadGames();
                int index = games.FindIndex(g => g.Id == Game.Id);
                if (index >= 0)
                {
                    if (!string.IsNullOrWhiteSpace(Game.BackupFolderPath))
                    {
                        if (!Directory.Exists(Game.BackupFolderPath))
                            Directory.CreateDirectory(Game.BackupFolderPath);
                        string infoFilePath = Path.Combine(Game.BackupFolderPath, "save_location.txt");
                        File.WriteAllText(infoFilePath, Game.SaveFolderPath);
                    }

                    Game = ViewModelHelpers.InputFormatter(Game);
                    games[index] = Game;
                    DataService.SaveGamesData(games);

                    series = Game.Series;
                    genre = Game.Genre;
                    theme = Game.Theme;

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
                    hasScore = newScore > 0;
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
            string url = "https://howlongtobeat.com/?q=" + Uri.EscapeDataString(Game.Name);
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch (Exception ex) { Console.WriteLine($"Không thể mở trình duyệt: {ex.Message}"); }
        }

        [RelayCommand]
        private void CheckMetaScore()
        {
            if (string.IsNullOrWhiteSpace(Game.SlugName)) return;
            string url = "https://www.metacritic.com/game/" + Uri.EscapeDataString(Game.SlugName) + "/";
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch (Exception ex) { Console.WriteLine($"Không thể mở trình duyệt: {ex.Message}"); }
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
                PreviewImage = LoadPreviewImage(Game.ImagePath);
                ReloadGameFromConfig();
                if (!IsReadOnly) SyncSelectedChipsFromGame();
            }
            IsReadOnly = true;
            StatusMessage = string.Empty;
        }

        [RelayCommand]
        private void Close() => RaiseRequestClose(false);

        // ==================== FOLDER COMMANDS ====================
        [RelayCommand]
        private void EditSaveFolder()
        {
            var d = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Choose Save Folder",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };
            if (d.ShowDialog() == true)
                Game.SaveFolderPath = d.FolderName;
        }

        [RelayCommand]
        private void EditBackupFolder()
        {
            var d = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Choose Backup Folder",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };
            if (d.ShowDialog() == true)
                Game.BackupFolderPath = d.FolderName;
        }

        [RelayCommand]
        private void OpenSaveFolder() => HandleFolderOpening((Game.SaveFolderPath, "Save Folder"));

        [RelayCommand]
        private void OpenBackupFolder() => HandleFolderOpening((Game.BackupFolderPath, "Backup Folder"));

        // ==================== IMAGE COMMANDS ====================
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
                PreviewImage = LoadPreviewImage(Game.ImagePath);
                StatusMessage = "Image preview updated successfully.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to load image: {ex.Message}";
            }
        }

        // ==================== PLAYTIME COMMANDS ====================
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
            Game.TotalPlayTimeSeconds = newSeconds < 0 ? 0 : newSeconds;
            UpdatePlayTimeFieldsFromSeconds();
            StatusMessage = $"Subtracted {AdjustMinutes} minutes from play time.";
            AdjustMinutes = 0;
        }

        // ==================== CHIP COMMANDS ====================
        [RelayCommand]
        private void OpenSeriesPopup()
        {
            SeriesSearchText = string.Empty;
            FilteredSeries = new ObservableCollection<string>(AllSeries);
            IsSeriesPopupOpen = true;
        }

        [RelayCommand]
        private void SelectSeries(string seriesName)
        {
            SelectedSeries = seriesName;
            IsSeriesPopupOpen = false;
        }

        [RelayCommand]
        private void CreateNewSeries()
        {
            if (!string.IsNullOrWhiteSpace(SeriesSearchText) && !AllSeries.Contains(SeriesSearchText))
            {
                AllSeries.Add(SeriesSearchText);
                SelectedSeries = SeriesSearchText;
            }
            IsSeriesPopupOpen = false;
        }

        [RelayCommand]
        private void RemoveSelectedSeries() => SelectedSeries = null;

        [RelayCommand]
        private void OpenGenresPopup()
        {
            GenresSearchText = string.Empty;
            FilteredGenres = new ObservableCollection<string>(AllGenres);
            IsGenresPopupOpen = true;
        }

        [RelayCommand]
        private void SelectGenre(string genreName)
        {
            if (!SelectedGenres.Contains(genreName))
                SelectedGenres.Add(genreName);
        }

        [RelayCommand]
        private void CreateNewGenre()
        {
            if (!string.IsNullOrWhiteSpace(GenresSearchText) && !AllGenres.Contains(GenresSearchText))
            {
                AllGenres.Add(GenresSearchText);
                if (!SelectedGenres.Contains(GenresSearchText))
                    SelectedGenres.Add(GenresSearchText);
            }
        }

        [RelayCommand]
        private void RemoveGenre(string genre)
        {
            if (SelectedGenres.Contains(genre))
                SelectedGenres.Remove(genre);
        }

        [RelayCommand]
        private void OpenThemesPopup()
        {
            ThemesSearchText = string.Empty;
            FilteredThemes = new ObservableCollection<string>(AllThemes);
            IsThemesPopupOpen = true;
        }

        [RelayCommand]
        private void SelectTheme(string themeName)
        {
            if (!SelectedThemes.Contains(themeName))
                SelectedThemes.Add(themeName);
        }

        [RelayCommand]
        private void CreateNewTheme()
        {
            if (!string.IsNullOrWhiteSpace(ThemesSearchText) && !AllThemes.Contains(ThemesSearchText))
            {
                AllThemes.Add(ThemesSearchText);
                if (!SelectedThemes.Contains(ThemesSearchText))
                    SelectedThemes.Add(ThemesSearchText);
            }
        }

        [RelayCommand]
        private void RemoveTheme(string theme)
        {
            if (SelectedThemes.Contains(theme))
                SelectedThemes.Remove(theme);
        }

        // ==================== PRIVATE HELPERS ====================
        private void InitializeMockData()
        {
            AllSeries = new ObservableCollection<string>(MockData.Series);
            AllGenres = new ObservableCollection<string>(MockData.Genres);
            AllThemes = new ObservableCollection<string>(MockData.Themes);

            SelectedGenres = new ObservableCollection<string>();
            SelectedThemes = new ObservableCollection<string>();
            FilteredSeries = new ObservableCollection<string>();
            FilteredGenres = new ObservableCollection<string>();
            FilteredThemes = new ObservableCollection<string>();
        }

        private void SyncSelectedChipsFromGame()
        {
            SelectedSeries = Game.Series;
            SelectedGenres.Clear();
            if (!string.IsNullOrWhiteSpace(Game.Genre))
            {
                var genres = Game.Genre.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(g => g.Trim());
                foreach (var g in genres) if (!string.IsNullOrWhiteSpace(g)) SelectedGenres.Add(g);
            }
            SelectedThemes.Clear();
            if (!string.IsNullOrWhiteSpace(Game.Theme))
            {
                var themes = Game.Theme.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(t => t.Trim());
                foreach (var t in themes) if (!string.IsNullOrWhiteSpace(t)) SelectedThemes.Add(t);
            }
        }

        private void LoadGameData()
        {
            SelectedSeries = Game.Series;
            SelectedGenres.Clear();
            if (!string.IsNullOrWhiteSpace(Game.Genre))
            {
                var genres = Game.Genre.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(g => g.Trim());
                foreach (var g in genres) if (!string.IsNullOrWhiteSpace(g)) SelectedGenres.Add(g);
            }
            SelectedThemes.Clear();
            if (!string.IsNullOrWhiteSpace(Game.Theme))
            {
                var themes = Game.Theme.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(t => t.Trim());
                foreach (var t in themes) if (!string.IsNullOrWhiteSpace(t)) SelectedThemes.Add(t);
            }
            SyncPublishDate(Game.PublishDate);
            UpdatePlayTimeFieldsFromSeconds();
            hasInitExeName = !string.IsNullOrWhiteSpace(Game.InitExeName);
        }

        private void UpdatePlayTimeFieldsFromSeconds()
        {
            var ts = TimeSpan.FromSeconds(Game.TotalPlayTimeSeconds);
            PlayTimeHours = (int)ts.TotalHours;
            PlayTimeMinutes = ts.Minutes;
            OnPropertyChanged(nameof(DisplayPlayTime));
        }

        private void UpdateTotalPlayTimeFromFields()
        {
            Game.TotalPlayTimeSeconds = (PlayTimeHours * 3600L) + (PlayTimeMinutes * 60L);
            OnPropertyChanged(nameof(DisplayPlayTime));
        }

        private BitmapImage? LoadPreviewImage(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            try { return new BitmapImage(new Uri(path)); }
            catch { return null; }
        }

        private void ReloadGameFromConfig()
        {
            var games = DataService.LoadGames();
            var reloaded = games.FirstOrDefault(g => g.Id == Game.Id);
            if (reloaded != null)
            {
                Game = reloaded;
                PreviewImage = LoadPreviewImage(Game.ImagePath);

                if (IsReadOnly)
                {
                    SaveFolderAccessible = !string.IsNullOrWhiteSpace(Game.SaveFolderPath) && Directory.Exists(Game.SaveFolderPath);
                    BackupFolderAccessible = !string.IsNullOrWhiteSpace(Game.BackupFolderPath) && Directory.Exists(Game.BackupFolderPath);
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
                try { Directory.Move(Game.BackupFolderPath, newBackupFolder); }
                catch (Exception ex) { StatusMessage = $"Failed to rename backup folder: {ex.Message}"; }
            }
            else if (!Directory.Exists(newBackupFolder))
                Directory.CreateDirectory(newBackupFolder);

            Game.BackupFolderPath = newBackupFolder;
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

        private void SyncPublishDate() => publishDateInput = Game.PublishDate?.ToString("dd/MM/yyyy") ?? "";

        // ==================== PARTIAL METHODS ====================
        partial void OnGameChanged(Game? value)
        {
            PreviewImage = LoadPreviewImage(value?.ImagePath);
            //SyncPublishDate();
            if (!IsReadOnly && !string.IsNullOrWhiteSpace(value?.Name))
                UpdateBackupFolderPath(value.Name);
            if (value == null)
            {
                publisher = developer = series = genre = theme = finishedIn = string.Empty;
                publishDate = null; metaScore = 0; excited = 0f; avgPlaytime = 0L; hasScore = false;
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
                hasScore = value.Score > 0f;
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
            OnPropertyChanged(nameof(HasSlugName));
            OnPropertyChanged(nameof(HasInitExeNameForView));
        }

        partial void OnIsReadOnlyChanged(bool value)
        {
            if (!value) UpdatePlayTimeFieldsFromSeconds();
        }

        partial void OnPlayTimeHoursChanged(int value)
        {
            if (value < 0) PlayTimeHours = 0;
            UpdateTotalPlayTimeFromFields();
        }

        partial void OnPlayTimeMinutesChanged(int value)
        {
            if (value < 0) PlayTimeMinutes = 0;
            if (value >= 60) { PlayTimeHours += value / 60; PlayTimeMinutes = value % 60; }
            UpdateTotalPlayTimeFromFields();
        }

        partial void OnPublisherChanged(string value) { Game.Publisher = value; OnPropertyChanged(nameof(DisplayPublisher)); }
        partial void OnDeveloperChanged(string value) { Game.Developer = value; OnPropertyChanged(nameof(DisplayDeveloper)); }
        partial void OnSeriesChanged(string value) { Game.Series = value; OnPropertyChanged(nameof(DisplaySeries)); }
        partial void OnGenreChanged(string value) { Game.Genre = value; OnPropertyChanged(nameof(DisplayGenre)); }
        partial void OnThemeChanged(string value) { Game.Theme = value; OnPropertyChanged(nameof(DisplayTheme)); }
        partial void OnExcitedChanged(float value) { Game.Excited = value; OnPropertyChanged(nameof(DisplayExcited)); }
        partial void OnFinishedInChanged(string value) { Game.FinishedIn = value; OnPropertyChanged(nameof(DisplayFinishedIn)); }
        partial void OnPublishDateChanged(DateOnly? value) { Game.PublishDate = value; OnPropertyChanged(nameof(DisplayPublishDate)); }
        partial void OnMetaScoreChanged(int value) { Game.MetaScore = value; OnPropertyChanged(nameof(DisplayMetaScore)); }
        partial void OnAvgPlaytimeChanged(long value) { Game.AvgPlayTime = value; OnPropertyChanged(nameof(DisplayAvgPlayTime)); }

        partial void OnSeriesSearchTextChanged(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                FilteredSeries = new ObservableCollection<string>(AllSeries);
            else
                FilteredSeries = new ObservableCollection<string>(AllSeries.Where(x => x.Contains(value, StringComparison.OrdinalIgnoreCase)));
            OnPropertyChanged(nameof(CreateNewSeriesText));
        }

        partial void OnGenresSearchTextChanged(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                FilteredGenres = new ObservableCollection<string>(AllGenres);
            else
                FilteredGenres = new ObservableCollection<string>(AllGenres.Where(x => x.Contains(value, StringComparison.OrdinalIgnoreCase)));
            OnPropertyChanged(nameof(CreateNewGenreText));
        }

        partial void OnThemesSearchTextChanged(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                FilteredThemes = new ObservableCollection<string>(AllThemes);
            else
                FilteredThemes = new ObservableCollection<string>(AllThemes.Where(x => x.Contains(value, StringComparison.OrdinalIgnoreCase)));
            OnPropertyChanged(nameof(CreateNewThemeText));
        }

        partial void OnHasInitExeNameChanged(bool value)
        {
            if (!value)
            {
                Game.InitExeName = string.Empty;
            }
            OnPropertyChanged(nameof(HasInitExeNameForView));
        }
    }
}