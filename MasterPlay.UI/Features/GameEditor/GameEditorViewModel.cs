using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MasterPlay.Core.Localization;
using MasterPlay.Infrastructure.FileSystem;
using MasterPlay.Infrastructure.Data.Services;
using MasterPlay.Infrastructure.Integrations.Cloudinary;
using MasterPlay.Infrastructure.Integrations.Rawg;
using MasterPlay.Core.Entities;
using MasterPlay.UI.Shared.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace MasterPlay.UI.Features.GameEditor
{
    public partial class GameEditorViewModel : BaseViewModel
    {
        private CancellationTokenSource? _msgCts;
        public GameEditorViewModel() : this(null) { }

        public GameEditorViewModel(Game? selectedGame)
        {
            IsNew = selectedGame == null;
            Game = selectedGame ?? new Game();
            ReloadGameFromConfig();
            LoadLists();
            LoadGameData();
            IsReadOnly = !IsNew;
            HasInitExeName = !string.IsNullOrWhiteSpace(Game.InitExeName);

            SelectedGenres.CollectionChanged += (s, e) => OnPropertyChanged(nameof(DisplayGenres));
            SelectedThemes.CollectionChanged += (s, e) => OnPropertyChanged(nameof(DisplayThemes));
        }
        private int? _originalProgressId;

        public bool IsNew { get; }
        public bool IsExisting => !IsNew;
        public bool CanDelete => IsExisting && !IsReadOnly;
        public bool CanFinish => IsExisting && IsReadOnly;
        public string EditorTitle => LocalizationService.Get(IsNew ? "AddGame" : "GameDetails");

        [ObservableProperty]
        private Game game;


        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayName))]
        private string name = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayPublisher))]
        private string publisher = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayDeveloper))]
        private string developer = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayExcited))]
        private double? excited;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayFinishedIn))]
        private string finishedIn = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayAvgPlayTime))]
        private int? avgPlaytime;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayMetaScore))]
        private int? metaScore;

        [ObservableProperty]
        private string slugName = string.Empty;

        [ObservableProperty]
        private string exeName = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayInitProcessName))]
        [NotifyPropertyChangedFor(nameof(HasInitExeNameForView))]
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

        public bool HasScore => (Score ?? 0) > 0;

        [ObservableProperty]
        private string publishDateInput = string.Empty;

        [ObservableProperty]
        private bool hasInitExeName;

        [ObservableProperty]
        private string statusMessage = string.Empty;


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

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplaySeries))]
        private Series? selectedSeries;

        [ObservableProperty]
        private ObservableCollection<Genre> selectedGenres = new();

        [ObservableProperty]
        private ObservableCollection<Theme> selectedThemes = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayOwnership))]
        private OwnershipStatus? selectedOwnership;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayProgress))]
        private ProgressStatus? selectedProgress;

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


        [ObservableProperty]
        private bool isEditSeriesPopupOpen;

        [ObservableProperty]
        private bool isEditGenrePopupOpen;

        [ObservableProperty]
        private bool isEditThemePopupOpen;

        [ObservableProperty]
        private Series? editingSeries;

        [ObservableProperty]
        private Genre? editingGenre;

        [ObservableProperty]
        private Theme? editingTheme;

        [ObservableProperty]
        private string editSeriesName = string.Empty;

        [ObservableProperty]
        private string editGenreName = string.Empty;

        [ObservableProperty]
        private string editThemeName = string.Empty;

        public string CreateNewSeriesText => string.IsNullOrWhiteSpace(SeriesSearchText) ? "" : LocalizationService.Get("CreateItem", SeriesSearchText);
        public string CreateNewGenreText => string.IsNullOrWhiteSpace(GenresSearchText) ? "" : LocalizationService.Get("CreateItem", GenresSearchText);
        public string CreateNewThemeText => string.IsNullOrWhiteSpace(ThemesSearchText) ? "" : LocalizationService.Get("CreateItem", ThemesSearchText);


        public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "--" : Name;
        public string DisplayPublishDate => Game.PublishDate?.ToString("MMM dd, yyyy") ?? "--";
        public string DisplayPublisher => string.IsNullOrWhiteSpace(Publisher) ? "--" : Publisher;
        public string DisplayDeveloper => string.IsNullOrWhiteSpace(Developer) ? "--" : Developer;
        public string DisplaySeries => SelectedSeries?.Name ?? "--";
        public string DisplayGenres => SelectedGenres.Any() ? string.Join(", ", SelectedGenres.Select(g => g.Name)) : "--";
        public string DisplayThemes => SelectedThemes.Any() ? string.Join(", ", SelectedThemes.Select(t => t.Name)) : "--";
        public string DisplayMetaScore => (MetaScore ?? 0) > 0 ? MetaScore.ToString() : "--";
        public string DisplayExcited => Excited switch
        {
            null => "--",
            <= 0 => "--",
            999 => "10🔥",
            _ => Excited.Value.ToString()
        };
        public string DisplayAvgPlayTime => (AvgPlaytime ?? 0) > 0 ? AvgPlaytime.ToString() + "h" : "--";
        public string DisplayFinishedIn => string.IsNullOrWhiteSpace(FinishedIn) ? "Not Finished" : FinishedIn;
        public string DisplayProcessName => string.IsNullOrWhiteSpace(ExeName) ? "--" : $"{ExeName}.exe";
        public string DisplayInitProcessName => string.IsNullOrWhiteSpace(InitExeName) ? "--" : $"{InitExeName}.exe";
        public string DisplaySaveFolderPath => string.IsNullOrWhiteSpace(SaveFolderPath) ? "--" : SaveFolderPath;
        public string DisplayBackupFolderPath => string.IsNullOrWhiteSpace(BackupFolderPath) ? "--" : BackupFolderPath;
        public string DisplayOwnership => SelectedOwnership?.Name ?? "--";
        public string DisplayProgress => SelectedProgress?.Name ?? "--";
        public string DisplayScore => Score switch
        {
            null => "--",
            <= 0 => "--",
            999 => "10🔥",
            _ => Score.Value.ToString()
        };
        public string DisplayPlayTime
        {
            get
            {
                if ((Game.TotalPlayTimeSeconds ?? 0) <= 0) return "--";
                var ts = TimeSpan.FromSeconds((double)Game.TotalPlayTimeSeconds);
                return $"{(int)ts.TotalHours}h{ts.Minutes:D2}m";
            }
        }

        public bool HasSlugName => !string.IsNullOrWhiteSpace(SlugName);
        public bool HasInitExeNameForView => !string.IsNullOrWhiteSpace(InitExeName);



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
            if (string.IsNullOrWhiteSpace(Name))
            {
                await ShowStatus(LocalizationService.Get("GameNameEmptyFirst"));
                return;
            }

            IsBusy = true;
            await ShowStatus(LocalizationService.Get("Syncing"), 0);

            try
            {
                var rawgGame = await RawgService.GetMetadata_Rawg(Name);

                if (rawgGame != null)
                {
                    Name = rawgGame.Name ?? Name;
                    Publisher = rawgGame.Publishers ?? string.Empty;
                    Developer = rawgGame.Developers ?? string.Empty;
                    SlugName = rawgGame.SlugName ?? string.Empty;
                    MetaScore = rawgGame.MetaScore;
                    AvgPlaytime = rawgGame.AvgPlaytime;

                    SyncPublishDate(ViewModelHelpers.StringToDateOnly(rawgGame.ReleaseDate));

                    await ShowStatus(LocalizationService.Get("SyncSuccessful"));
                }
                else
                {
                    await ShowStatus(LocalizationService.Get("CannotFindGameInfo", Name));
                }
            }
            catch (Exception ex)
            {
                await ShowStatus(LocalizationService.Get("SyncError", ex.Message));
                Debug.WriteLine($"Sync error: {ex}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task SyncCover()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                await ShowStatus(LocalizationService.Get("GameNameEmpty"));
                return;
            }

            IsBusy = true;
            try
            {
                var metadata = await RawgService.GetMetadata_Rawg(Name);
                if (metadata != null && !string.IsNullOrWhiteSpace(metadata.Background_Image))
                {
                    ImagePath = metadata.Background_Image;
                    SyncImageCommand.Execute(null);
                    await ShowStatus(LocalizationService.Get("CoverUpdated"));
                }
                else
                {
                    await ShowStatus(LocalizationService.Get("CoverNotFound"));
                }
            }
            catch (Exception ex)
            {
                await ShowStatus(LocalizationService.Get("ErrorWithDetails", ex.Message));
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task SyncGallery()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                await ShowStatus(LocalizationService.Get("GameNameEmpty"));
                return;
            }

            IsBusy = true;
            try
            {
                var galleryUrl = await RawgService.GetGalleryImage(Name);
                if (!string.IsNullOrWhiteSpace(galleryUrl))
                {
                    GImagePath = galleryUrl;
                    await ShowStatus(LocalizationService.Get("GalleryUpdated"));
                }
                else
                {
                    await ShowStatus(LocalizationService.Get("GalleryNotFound"));
                }
            }
            catch (Exception ex)
            {
                await ShowStatus(LocalizationService.Get("ErrorWithDetails", ex.Message));
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void SyncPublishDate(DateOnly? date)
        {
            PublishDateInput = date?.ToString("dd/MM/yyyy") ?? string.Empty;
            Game.PublishDate = date;
        }

        [RelayCommand]
        private async Task GenerateBackupFolder()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                await ShowStatus(LocalizationService.Get("GameNameEmpty"));
                return;
            }

            IsBusy = true;
            bool result = DataService.CreateBackupFolder(Game);
            if (result)
            {
                BackupFolderPath = Game.BackupFolderPath;
                await ShowStatus(LocalizationService.Get("BackupFolderCreated"));
            }
            else
            {
                await ShowStatus(LocalizationService.Get("BackupFolderCreateFailed"));
            }
            IsBusy = false;
        }

        private void UpdateBackupFolderPath(string newGameName)
        {
            if (!string.IsNullOrWhiteSpace(Game.BackupFolderPath) && Directory.Exists(Game.BackupFolderPath))
            {
                bool renamed = DataService.RenameBackupFolder(Game, newGameName);
                if (renamed)
                {
                    BackupFolderPath = Game.BackupFolderPath;
                }
                else
                {
                    StatusMessage = LocalizationService.Get("BackupFolderRenameFailed");
                }
            }
        }

        [RelayCommand]
        private async Task Save()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(Name) && Name != Game.Name)
                {
                    UpdateBackupFolderPath(Name);
                }

                Game.SeriesId = SelectedSeries?.Id;
                Game.OwnershipId = SelectedOwnership?.Id;
                Game.ProgressId = SelectedProgress?.Id;

                if (SelectedProgress?.Id == 3 && _originalProgressId != 3)
                {
                    DataService.ReorderOnPromoteToNextPlay(Game);
                }

                if (Game.GameGenres == null)
                    Game.GameGenres = new List<GameGenre>();
                Game.GameGenres.Clear();
                foreach (var genre in SelectedGenres)
                {
                    Game.GameGenres.Add(new GameGenre { GameId = Game.Id, GenreId = genre.Id });
                }

                if (Game.GameThemes == null)
                    Game.GameThemes = new List<GameTheme>();
                Game.GameThemes.Clear();
                foreach (var theme in SelectedThemes)
                {
                    Game.GameThemes.Add(new GameTheme { GameId = Game.Id, ThemeId = theme.Id });
                }

                Game.Name = Name;
                Game.Publisher = Publisher;
                Game.Developer = Developer;
                Game.Excited = Excited;
                Game.FinishedIn = FinishedIn;
                Game.MetaScore = MetaScore;
                Game.AvgPlayTime = AvgPlaytime;
                Game.SlugName = SlugName;
                Game.ExeName = ExeName;
                Game.InitExeName = InitExeName;
                Game.SaveFolderPath = SaveFolderPath;
                Game.BackupFolderPath = BackupFolderPath;
                Game.ImagePath = ImagePath;
                Game.GImagePath = GImagePath;
                Game.Score = Score;




                try
                {
                    IsBusy = true;

                    if (!string.IsNullOrWhiteSpace(Game.ImagePath) &&
                        !CloudinaryImageService.IsCloudinaryUrl(Game.ImagePath))
                    {
                        await ShowStatus(LocalizationService.Get("UploadingCover"), 0);
                        var coverUrl = await CloudinaryImageService.UploadGameImageAsync(Game.Id, Game.ImagePath, "cover");
                        if (!string.IsNullOrWhiteSpace(coverUrl))
                        {
                            Game.ImagePath = coverUrl;
                            ImagePath = coverUrl;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(Game.GImagePath) &&
                        !CloudinaryImageService.IsCloudinaryUrl(Game.GImagePath))
                    {
                        await ShowStatus(LocalizationService.Get("UploadingGallery"), 0);
                        var galleryUrl = await CloudinaryImageService.UploadGameImageAsync(Game.Id, Game.GImagePath, "gallery");
                        if (!string.IsNullOrWhiteSpace(galleryUrl))
                        {
                            Game.GImagePath = galleryUrl;
                            GImagePath = galleryUrl;
                        }
                    }
                }
                catch (Exception cloudEx)
                {

                    Debug.WriteLine($"Cloudinary upload error: {cloudEx.Message}");
                }
                finally
                {
                    IsBusy = false;
                }


                Game = ViewModelHelpers.InputFormatter(Game);

                if (IsNew)
                {
                    DataService.AddGame(Game);
                    DataService.CreateBackupFolder(Game);
                }
                else
                {
                    DataService.UpdateGame(Game);
                }

                if (!string.IsNullOrWhiteSpace(Game.BackupFolderPath) && FileHelper.FileCheck(Game.BackupFolderPath))
                {
                    DataService.UpdateSaveLocationFile(Game);
                }

                IsReadOnly = true;
                ReloadGameFromConfig();

                await ShowStatus(LocalizationService.Get("DataSaved"));
                if (IsNew)
                    RaiseRequestClose(true);
            }
            catch (Exception ex)
            {
                await ShowStatus(LocalizationService.Get("ErrorWithDetails", ex.Message));
                MessageBox.Show(StatusMessage, LocalizationService.Get("SystemError"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            if (IsNew)
            {
                RaiseRequestClose(false);
                return;
            }
            IsReadOnly = true;
            ReloadGameFromConfig();
            StatusMessage = string.Empty;
        }

        [RelayCommand]
        private async Task FinishGame()
        {
            try
            {
                FinishScoreInput = string.Empty;
                IsFinishPopupOpen = true;
            }
            catch (Exception ex)
            {
                await ShowStatus(LocalizationService.Get("ErrorWithDetails", ex.Message));
            }
        }

        [RelayCommand]
        private async Task FinishSave()
        {
            try
            {
                if (double.TryParse(FinishScoreInput, out double newScore))
                {
                    if (newScore < 0 || newScore > 100 && newScore != 999)
                    {
                        await ShowStatus(LocalizationService.Get("ScoreRange"));
                        return;
                    }

                    Score = newScore;
                    SelectedProgress = AllProgresses.FirstOrDefault(p => p.Name == "Done") ?? AllProgresses.FirstOrDefault();
                    FinishedIn = DateTime.Now.Year.ToString();
                    await Save();
                    IsFinishPopupOpen = false;
                    await ShowStatus(LocalizationService.Get("GameFinished", Name, newScore));
                }
                else
                {
                    await ShowStatus(LocalizationService.Get("InvalidScore"));
                }
            }
            catch (Exception ex)
            {
                await ShowStatus(LocalizationService.Get("ErrorWithDetails", ex.Message));
            }
        }

        [RelayCommand]
        private void FinishCancel()
        {
            IsFinishPopupOpen = false;
        }

        [RelayCommand]
        private async Task DeleteGame()
        {
            var result = MessageBox.Show(LocalizationService.Get("DisableConfirmation", Name),
                LocalizationService.Get("Confirm"), MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    Game.IsDisabled = true;
                    DataService.UpdateGame(Game);
                    await ShowStatus(LocalizationService.Get("GameDisabled", Name));
                    RaiseRequestClose(true);
                }
                catch (Exception ex)
                {
                    await ShowStatus(LocalizationService.Get("ErrorWithDetails", ex.Message));
                    MessageBox.Show(StatusMessage, LocalizationService.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private void Close() => RaiseRequestClose(false);

        [RelayCommand]
        private void EditSaveFolder()
        {
            var d = new Microsoft.Win32.OpenFolderDialog
            {
                Title = LocalizationService.Get("ChooseSaveFolder"),
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };
            if (d.ShowDialog() == true)
                SaveFolderPath = d.FolderName;
        }

        [RelayCommand]
        private void EditBackupFolder()
        {
            var d = new Microsoft.Win32.OpenFolderDialog
            {
                Title = LocalizationService.Get("ChooseBackupFolder"),
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };
            if (d.ShowDialog() == true)
                BackupFolderPath = d.FolderName;
        }

        [RelayCommand]
        private void OpenSaveFolder() => HandleFolderOpening((SaveFolderPath, LocalizationService.Get("SaveFolder")));

        [RelayCommand]
        private void OpenBackupFolder() => HandleFolderOpening((BackupFolderPath, LocalizationService.Get("BackupFolder")));

        [RelayCommand]
        private async Task SyncImage()
        {
            if (string.IsNullOrWhiteSpace(ImagePath))
            {
                PreviewImage = null;
                await ShowStatus(LocalizationService.Get("NoImageUrl"));
                return;
            }
            try
            {
                PreviewImage = LoadPreviewImage(ImagePath);
                await ShowStatus(LocalizationService.Get("ImagePreviewUpdated"));
            }
            catch (Exception ex)
            {
                await ShowStatus(LocalizationService.Get("ImageLoadFailed", ex.Message));
            }
        }

        [RelayCommand]
        private async Task AdjustPlayTimeUp()
        {
            if (AdjustMinutes <= 0)
            {
                await ShowStatus(LocalizationService.Get("PositiveMinutes"));
                return;
            }
            Game.TotalPlayTimeSeconds = (Game.TotalPlayTimeSeconds ?? 0) + AdjustMinutes * 60;
            UpdatePlayTimeFieldsFromSeconds();
            await ShowStatus(LocalizationService.Get("MinutesAdded", AdjustMinutes));
            AdjustMinutes = 0;
        }

        [RelayCommand]
        private async Task AdjustPlayTimeDown()
        {
            if (AdjustMinutes <= 0)
            {
                await ShowStatus(LocalizationService.Get("PositiveMinutes"));
                return;
            }
            long newSeconds = (Game.TotalPlayTimeSeconds ?? 0) - AdjustMinutes * 60;
            Game.TotalPlayTimeSeconds = newSeconds < 0 ? 0 : (int)newSeconds;
            UpdatePlayTimeFieldsFromSeconds();
            await ShowStatus(LocalizationService.Get("MinutesSubtracted", AdjustMinutes));
            AdjustMinutes = 0;
        }

        [RelayCommand]
        private void CheckPlayTime()
        {
            if (string.IsNullOrWhiteSpace(Name)) return;
            string url = "https://howlongtobeat.com/?q=" + Uri.EscapeDataString(Name);
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
            OnPropertyChanged(nameof(DisplaySeries));
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
                OnPropertyChanged(nameof(DisplaySeries));
            }
            IsSeriesPopupOpen = false;
        }

        [RelayCommand]
        private void RemoveSelectedSeries()
        {
            SelectedSeries = null;
            OnPropertyChanged(nameof(DisplaySeries));
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
            OnPropertyChanged(nameof(DisplayGenres));
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
                OnPropertyChanged(nameof(DisplayGenres));
            }
        }

        [RelayCommand]
        private void RemoveGenre(Genre genre)
        {
            if (SelectedGenres.Contains(genre))
                SelectedGenres.Remove(genre);
            OnPropertyChanged(nameof(DisplayGenres));
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
            OnPropertyChanged(nameof(DisplayThemes));
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
                OnPropertyChanged(nameof(DisplayThemes));
            }
        }

        [RelayCommand]
        private void RemoveTheme(Theme theme)
        {
            if (SelectedThemes.Contains(theme))
                SelectedThemes.Remove(theme);
            OnPropertyChanged(nameof(DisplayThemes));
        }


        [RelayCommand]
        private void OpenEditSeriesPopup(Series series)
        {
            IsSeriesPopupOpen = false;
            EditingSeries = series;
            EditSeriesName = series.Name;
            IsEditSeriesPopupOpen = true;
        }

        [RelayCommand]
        private void OpenEditGenrePopup(Genre genre)
        {
            IsGenresPopupOpen = false;
            EditingGenre = genre;
            EditGenreName = genre.Name;
            IsEditGenrePopupOpen = true;
        }

        [RelayCommand]
        private void OpenEditThemePopup(Theme theme)
        {
            IsThemesPopupOpen = false;
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
                MessageBox.Show(LocalizationService.Get("SeriesNameEmpty"), LocalizationService.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var success = DataService.UpdateSeries(EditingSeries.Id, EditSeriesName.Trim());
            if (success)
            {
                EditingSeries.Name = EditSeriesName.Trim();

                AllSeries = new ObservableCollection<Series>(AllSeries.OrderBy(s => s.Name));
                FilteredSeries = new ObservableCollection<Series>(AllSeries);
                OnPropertyChanged(nameof(AllSeries));
                OnPropertyChanged(nameof(FilteredSeries));

                if (SelectedSeries?.Id == EditingSeries.Id)
                    OnPropertyChanged(nameof(DisplaySeries));
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
                MessageBox.Show(LocalizationService.Get("GenreNameEmpty"), LocalizationService.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var success = DataService.UpdateGenre(EditingGenre.Id, EditGenreName.Trim());
            if (success)
            {
                EditingGenre.Name = EditGenreName.Trim();
                AllGenres = new ObservableCollection<Genre>(AllGenres.OrderBy(g => g.Name));
                FilteredGenres = new ObservableCollection<Genre>(AllGenres);
                OnPropertyChanged(nameof(AllGenres));
                OnPropertyChanged(nameof(FilteredGenres));
                if (SelectedGenres.Any(g => g.Id == EditingGenre.Id))
                    OnPropertyChanged(nameof(DisplayGenres));
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
                MessageBox.Show(LocalizationService.Get("ThemeNameEmpty"), LocalizationService.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var success = DataService.UpdateTheme(EditingTheme.Id, EditThemeName.Trim());
            if (success)
            {
                EditingTheme.Name = EditThemeName.Trim();
                AllThemes = new ObservableCollection<Theme>(AllThemes.OrderBy(t => t.Name));
                FilteredThemes = new ObservableCollection<Theme>(AllThemes);
                OnPropertyChanged(nameof(AllThemes));
                OnPropertyChanged(nameof(FilteredThemes));
                if (SelectedThemes.Any(t => t.Id == EditingTheme.Id))
                    OnPropertyChanged(nameof(DisplayThemes));
            }
            IsEditThemePopupOpen = false;
            EditingTheme = null;
        }

        [RelayCommand]
        private void DeleteSeries()
        {
            if (EditingSeries == null) return;
            var result = MessageBox.Show(LocalizationService.Get("DeleteAssociationConfirmation", EditingSeries.Name),
                                         LocalizationService.Get("ConfirmDelete"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
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
                OnPropertyChanged(nameof(FilteredSeries));
                OnPropertyChanged(nameof(DisplaySeries));
            }
            IsEditSeriesPopupOpen = false;
            EditingSeries = null;
        }

        [RelayCommand]
        private void DeleteGenre()
        {
            if (EditingGenre == null) return;
            var result = MessageBox.Show(LocalizationService.Get("DeleteAssociationConfirmation", EditingGenre.Name),
                                         LocalizationService.Get("ConfirmDelete"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
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
                OnPropertyChanged(nameof(FilteredGenres));
                OnPropertyChanged(nameof(DisplayGenres));
            }
            IsEditGenrePopupOpen = false;
            EditingGenre = null;
        }

        [RelayCommand]
        private void DeleteTheme()
        {
            if (EditingTheme == null) return;
            var result = MessageBox.Show(LocalizationService.Get("DeleteAssociationConfirmation", EditingTheme.Name),
                                         LocalizationService.Get("ConfirmDelete"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
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
                OnPropertyChanged(nameof(FilteredThemes));
                OnPropertyChanged(nameof(DisplayThemes));
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

        private void LoadGameData()
        {
            SelectedSeries = AllSeries.FirstOrDefault(s => s.Id == Game.SeriesId);
            OnPropertyChanged(nameof(DisplaySeries));

            SelectedGenres.Clear();
            if (Game.GameGenres != null)
            {
                foreach (var gg in Game.GameGenres)
                {
                    var genre = AllGenres.FirstOrDefault(g => g.Id == gg.GenreId);
                    if (genre != null) SelectedGenres.Add(genre);
                }
            }
            OnPropertyChanged(nameof(DisplayGenres));

            SelectedThemes.Clear();
            if (Game.GameThemes != null)
            {
                foreach (var gt in Game.GameThemes)
                {
                    var theme = AllThemes.FirstOrDefault(t => t.Id == gt.ThemeId);
                    if (theme != null) SelectedThemes.Add(theme);
                }
            }
            OnPropertyChanged(nameof(DisplayThemes));

            SelectedOwnership = AllOwnerships.FirstOrDefault(o => o.Id == Game.OwnershipId);
            OnPropertyChanged(nameof(DisplayOwnership));
            SelectedProgress = AllProgresses.FirstOrDefault(p => p.Id == Game.ProgressId);
            OnPropertyChanged(nameof(DisplayProgress));

            _originalProgressId = Game.ProgressId;

            Name = Game.Name ?? string.Empty;
            Publisher = Game.Publisher ?? string.Empty;
            Developer = Game.Developer ?? string.Empty;
            Excited = Game.Excited;
            FinishedIn = Game.FinishedIn ?? string.Empty;
            AvgPlaytime = Game.AvgPlayTime;
            MetaScore = Game.MetaScore;
            SlugName = Game.SlugName ?? string.Empty;
            ExeName = Game.ExeName ?? string.Empty;
            InitExeName = Game.InitExeName ?? string.Empty;
            SaveFolderPath = Game.SaveFolderPath ?? string.Empty;
            BackupFolderPath = Game.BackupFolderPath ?? string.Empty;
            ImagePath = Game.ImagePath ?? string.Empty;
            GImagePath = Game.GImagePath ?? string.Empty;
            Score = Game.Score;
            SyncPublishDate(Game.PublishDate);
            UpdatePlayTimeFieldsFromSeconds();
            HasInitExeName = !string.IsNullOrWhiteSpace(Game.InitExeName);
        }

        private void UpdatePlayTimeFieldsFromSeconds()
        {
            var ts = TimeSpan.FromSeconds((double)(Game.TotalPlayTimeSeconds ?? 0));
            PlayTimeHours = (int)ts.TotalHours;
            PlayTimeMinutes = ts.Minutes;
            OnPropertyChanged(nameof(DisplayPlayTime));
        }

        private void UpdateTotalPlayTimeFromFields()
        {
            Game.TotalPlayTimeSeconds = (int)(PlayTimeHours * 3600L + PlayTimeMinutes * 60L);
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
                }

                LoadGameData();
            }
        }

        private void HandleFolderOpening(params (string? Path, string Desc)[] locations)
        {
            if (!FileHelper.TryOpenLocations(out _, locations))
            {
                MessageBox.Show(
                    LocalizationService.Get("FolderUnavailable"),
                    LocalizationService.Get("AccessError"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

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



        partial void OnGameChanged(Game value)
        {
            PreviewImage = LoadPreviewImage(value?.ImagePath);
        }

        partial void OnIsReadOnlyChanged(bool value)
        {
            OnPropertyChanged(nameof(CanDelete));
            OnPropertyChanged(nameof(CanFinish));
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

        partial void OnNameChanged(string value) => Game.Name = value;
        partial void OnPublisherChanged(string value) => Game.Publisher = value;
        partial void OnDeveloperChanged(string value) => Game.Developer = value;
        partial void OnExcitedChanged(double? value) => Game.Excited = value;
        partial void OnFinishedInChanged(string value) => Game.FinishedIn = value;
        partial void OnAvgPlaytimeChanged(int? value) => Game.AvgPlayTime = value;
        partial void OnMetaScoreChanged(int? value) => Game.MetaScore = value;
        partial void OnExeNameChanged(string value) => Game.ExeName = value;

        partial void OnSaveFolderPathChanged(string value) => Game.SaveFolderPath = value;
        partial void OnBackupFolderPathChanged(string value) => Game.BackupFolderPath = value;
        partial void OnImagePathChanged(string value)
        {
            Game.ImagePath = value;
            PreviewImage = LoadPreviewImage(value);
        }
        partial void OnGImagePathChanged(string value) => Game.GImagePath = value;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasScore))]
        [NotifyPropertyChangedFor(nameof(DisplayScore))]
        private double? score;

        partial void OnScoreChanged(double? value)
        {
            Game.Score = value;
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
            Game.InitExeName = value;
            OnPropertyChanged(nameof(HasInitExeNameForView));
        }

        partial void OnHasInitExeNameChanged(bool value)
        {
            if (!value) InitExeName = string.Empty;
            OnPropertyChanged(nameof(HasInitExeNameForView));
        }

        [ObservableProperty]
        private bool isFinishPopupOpen;

        [ObservableProperty]
        private string finishScoreInput = string.Empty;
    }
}
