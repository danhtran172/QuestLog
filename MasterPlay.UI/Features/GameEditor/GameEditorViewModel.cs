using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MasterPlay.Core.Localization;
using MasterPlay.Infrastructure.FileSystem;
using MasterPlay.Infrastructure.Data.Services;
using MasterPlay.Infrastructure.Integrations.Cloudinary;
using MasterPlay.Infrastructure.Integrations.Gemini;
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
            if (IsNew)
                Game.ProgressId = AllProgresses.FirstOrDefault(progress => string.Equals(progress.Name, "Pending", StringComparison.OrdinalIgnoreCase))?.Id;
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
        public bool CanFinish => IsExisting && IsReadOnly && !IsDlc;
        public string EditorTitle => LocalizationService.Get(IsNew ? "AddGame" : "GameDetails");
        public bool ShowRuntimeFields => !IsDlc;
        public bool ShowSeriesField => !IsDlc;
        public bool ShowBaseGameField => IsDlc;
        public bool ShowInitExeNameField => ShowRuntimeFields && HasInitExeName;
        public string DisplayBaseGame => SelectedBaseGame?.Name ?? "--";

        [ObservableProperty]
        private Game game;


        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayName))]
        [NotifyPropertyChangedFor(nameof(HasPreviewHeader))]
        private string name = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanFinish))]
        [NotifyPropertyChangedFor(nameof(ShowRuntimeFields))]
        [NotifyPropertyChangedFor(nameof(ShowSeriesField))]
        [NotifyPropertyChangedFor(nameof(ShowBaseGameField))]
        [NotifyPropertyChangedFor(nameof(ShowInitExeNameField))]
        private bool isDlc;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasUbisoftPublisher))]
        [NotifyPropertyChangedFor(nameof(DisplayPublisher))]
        private string publisher = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasUbisoftPublisher))]
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
        [NotifyPropertyChangedFor(nameof(DisplayProcessName))]
        private string exeName = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayInitProcessName))]
        [NotifyPropertyChangedFor(nameof(HasInitExeNameForView))]
        private string initExeName = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplaySaveFolderPath))]
        private string saveFolderPath = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayBackupFolderPath))]
        private string backupFolderPath = string.Empty;

        [ObservableProperty]
        private string imagePath = string.Empty;

        [ObservableProperty]
        private string gImagePath = string.Empty;

        [ObservableProperty]
        private string steamLibraryImagePath = string.Empty;

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasPreviewHeader))]
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
        [NotifyPropertyChangedFor(nameof(ShowInitExeNameField))]
        private bool hasInitExeName;

        [ObservableProperty]
        private string statusMessage = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSteamAccountData))]
        private string steamAccount = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSteamAccountData))]
        private string steamPassword = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasUbisoftAccountData))]
        private string ubisoftAccount = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasUbisoftAccountData))]
        private string ubisoftPassword = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSupportDocument))]
        private string supportDocumentUrl = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasDownloadLink))]
        private string downloadUrl = string.Empty;

        [ObservableProperty]
        private string ownershipNote = string.Empty;

        [ObservableProperty]
        private bool isOwnershipInfoVisible;


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
        private ObservableCollection<Game> baseGames = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplaySeries))]
        private Series? selectedSeries;

        [ObservableProperty]
        private ObservableCollection<Genre> selectedGenres = new();

        [ObservableProperty]
        private ObservableCollection<Theme> selectedThemes = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayOwnership))]
        [NotifyPropertyChangedFor(nameof(IsSteamOffOwnership))]
        [NotifyPropertyChangedFor(nameof(IsPiratedOwnership))]
        [NotifyPropertyChangedFor(nameof(HasSpecialOwnership))]
        private OwnershipStatus? selectedOwnership;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayProgress))]
        private ProgressStatus? selectedProgress;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayBaseGame))]
        private Game? selectedBaseGame;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsAnyPopupOpen))]
        private bool isSeriesPopupOpen;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsAnyPopupOpen))]
        private bool isGenresPopupOpen;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsAnyPopupOpen))]
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
        [NotifyPropertyChangedFor(nameof(IsAnyPopupOpen))]
        private bool isEditSeriesPopupOpen;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsAnyPopupOpen))]
        private bool isEditGenrePopupOpen;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsAnyPopupOpen))]
        private bool isEditThemePopupOpen;

        public bool IsAnyPopupOpen =>
            IsEditThemePopupOpen ||
            IsEditGenrePopupOpen ||
            IsEditSeriesPopupOpen ||
            IsFinishPopupOpen ||
            IsThemesPopupOpen ||
            IsGenresPopupOpen ||
            IsSeriesPopupOpen;

        public bool CloseTopPopup()
        {
            if (IsEditThemePopupOpen)
            {
                IsEditThemePopupOpen = false;
                return true;
            }

            if (IsEditGenrePopupOpen)
            {
                IsEditGenrePopupOpen = false;
                return true;
            }

            if (IsEditSeriesPopupOpen)
            {
                IsEditSeriesPopupOpen = false;
                return true;
            }

            if (IsFinishPopupOpen)
            {
                IsFinishPopupOpen = false;
                return true;
            }

            if (IsThemesPopupOpen)
            {
                IsThemesPopupOpen = false;
                return true;
            }

            if (IsGenresPopupOpen)
            {
                IsGenresPopupOpen = false;
                return true;
            }

            if (IsSeriesPopupOpen)
            {
                IsSeriesPopupOpen = false;
                return true;
            }

            return false;
        }

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
        public bool HasPreviewHeader => !IsNew || PreviewImage != null;
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
            999 => "10",
            _ => Excited.Value.ToString()
        };
        public string DisplayAvgPlayTime => (AvgPlaytime ?? 0) > 0 ? AvgPlaytime.ToString() + "h" : "--";
        public string DisplayFinishedIn => string.IsNullOrWhiteSpace(FinishedIn) ? "Not Finished" : FinishedIn;

        public string DisplayFinishHistory
        {
            get
            {
                var finishes = Game?.Finishes?
                    .OrderByDescending(finish => finish.FinishedAt)
                    .Select(finish => $"{finish.FinishedAt:yyyy-MM-dd} \u00b7 {FormatFinishScore(finish.Score)}")
                    .ToList();

                return finishes?.Count > 0
                    ? string.Join(Environment.NewLine, finishes)
                    : "Not Finished";
            }
        }

        private static string FormatFinishScore(double? score) => score switch
        {
            null or <= 0 => "--",
            999 => "10",
            _ => score.Value.ToString()
        };
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
            999 => "10",
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
        public bool IsSteamOffOwnership => SelectedOwnership?.Id == 6;
        public bool IsPiratedOwnership => SelectedOwnership?.Id == 2;
        public bool HasSpecialOwnership => IsSteamOffOwnership || IsPiratedOwnership;
        public bool HasUbisoftPublisher =>
            Publisher.Contains("Ubisoft", StringComparison.OrdinalIgnoreCase) ||
            Developer.Contains("Ubisoft", StringComparison.OrdinalIgnoreCase);
        public bool HasSteamAccountData =>
            !string.IsNullOrWhiteSpace(SteamAccount) ||
            !string.IsNullOrWhiteSpace(SteamPassword);
        public bool HasUbisoftAccountData =>
            !string.IsNullOrWhiteSpace(UbisoftAccount) ||
            !string.IsNullOrWhiteSpace(UbisoftPassword);
        public bool HasSupportDocument => !string.IsNullOrWhiteSpace(SupportDocumentUrl);
        public bool HasDownloadLink => !string.IsNullOrWhiteSpace(DownloadUrl);



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
        private void ToggleOwnershipInfo()
        {
            if (HasSpecialOwnership)
                IsOwnershipInfoVisible = !IsOwnershipInfoVisible;
        }

        [RelayCommand]
        private async Task CopyOwnershipValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            try
            {
                Clipboard.SetText(value);
                await ShowStatus(LocalizationService.Get("CopiedToClipboard"));
            }
            catch
            {
                await ShowStatus(LocalizationService.Get("ClipboardUnavailable"));
            }
        }

        [RelayCommand]
        private async Task OpenOwnershipLink(string? value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                await ShowStatus(LocalizationService.Get("InvalidLink"));
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch
            {
                await ShowStatus(LocalizationService.Get("CannotOpenLink"));
            }
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

                    if (!string.IsNullOrWhiteSpace(rawgGame.Background_Image))
                        ImagePath = rawgGame.Background_Image;

                    try
                    {
                        var galleryImage = await RawgService.GetGalleryImage(Name);
                        if (!string.IsNullOrWhiteSpace(galleryImage))
                            GImagePath = galleryImage;

                        var steamLibraryImage = await RawgService.GetSteamLibraryImage(Name);
                        if (!string.IsNullOrWhiteSpace(steamLibraryImage))
                            SteamLibraryImagePath = steamLibraryImage;
                    }
                    catch (Exception galleryException)
                    {
                        Debug.WriteLine($"Gallery sync skipped: {galleryException}");
                    }

                    // Gemini enrichment is optional: a quota/API/model failure must not fail metadata sync.
                    var executableName = await GeminiService.GetExecutableNameAsync(Name);
                    if (!string.IsNullOrWhiteSpace(executableName))
                        ExeName = executableName;

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

        [RelayCommand]
        private async Task SyncSteamLibraryImage()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                await ShowStatus(LocalizationService.Get("GameNameEmpty"));
                return;
            }

            IsBusy = true;
            try
            {
                var steamLibraryUrl = await RawgService.GetSteamLibraryImage(Name);
                if (!string.IsNullOrWhiteSpace(steamLibraryUrl))
                {
                    SteamLibraryImagePath = steamLibraryUrl;
                    await ShowStatus(LocalizationService.Get("SteamLibraryImageUpdated"));
                }
                else
                {
                    await ShowStatus(LocalizationService.Get("SteamLibraryImageNotFound"));
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
            if (IsDlc)
                return;

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
                var publishDate = ViewModelHelpers.StringToDateOnly(PublishDateInput);
                if (!string.IsNullOrWhiteSpace(PublishDateInput) && publishDate == null)
                {
                    await ShowStatus(LocalizationService.Get("InvalidPublishDate"));
                    return;
                }

                Game.PublishDate = publishDate;

                if (IsDlc)
                {
                    if (SelectedBaseGame == null)
                    {
                        await ShowStatus(LocalizationService.Get("DlcBaseGameRequired"));
                        return;
                    }

                    Name = EnsureDlcNameTag(Name);
                    ClearDlcRuntimeData();
                    Game.BaseGameId = SelectedBaseGame.Id;
                    Game.SeriesId = SelectedBaseGame.SeriesId;
                }

                if (!IsDlc && !string.IsNullOrWhiteSpace(Name) && Name != Game.Name)
                {
                    UpdateBackupFolderPath(Name);
                }

                if (!IsDlc)
                    Game.SeriesId = SelectedSeries?.Id;
                Game.OwnershipId = SelectedOwnership?.Id;
                Game.ProgressId = SelectedProgress?.Id;

                if (!IsDlc && SelectedProgress?.Id == 3 && _originalProgressId != 3)
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
                Game.SteamLibraryImagePath = SteamLibraryImagePath;
                Game.Score = Score;
                Game.OwnershipInfo = CreateOwnershipInfo();




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

                    if (!string.IsNullOrWhiteSpace(Game.SteamLibraryImagePath) &&
                        !CloudinaryImageService.IsCloudinaryUrl(Game.SteamLibraryImagePath))
                    {
                        await ShowStatus(LocalizationService.Get("UploadingSteamLibraryImage"), 0);
                        var steamLibraryUrl = await CloudinaryImageService.UploadGameImageAsync(Game.Id, Game.SteamLibraryImagePath, "steam-library");
                        if (!string.IsNullOrWhiteSpace(steamLibraryUrl))
                        {
                            Game.SteamLibraryImagePath = steamLibraryUrl;
                            SteamLibraryImagePath = steamLibraryUrl;
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
                    if (!IsDlc)
                        DataService.CreateBackupFolder(Game);
                }
                else
                {
                    DataService.UpdateGame(Game);
                }

                if (!IsDlc && !string.IsNullOrWhiteSpace(Game.BackupFolderPath) && FileHelper.FileCheck(Game.BackupFolderPath))
                {
                    DataService.UpdateSaveLocationFile(Game);
                }

                IsReadOnly = true;
                ReloadGameFromConfig();
                RefreshViewState();

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
            RefreshViewState();
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
                    DataService.AddGameFinish(Game.Id, newScore, DateTime.Now);
                    ReloadGameFromConfig();
                    OnPropertyChanged(nameof(DisplayFinishHistory));
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
            BaseGames = new ObservableCollection<Game>(DataService.LoadGames()
                .Where(game => !game.IsDlc && game.Id != Game.Id)
                .OrderBy(game => game.Name));

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

            IsDlc = Game.IsDlc;
            SelectedBaseGame = BaseGames.FirstOrDefault(game => game.Id == Game.BaseGameId);

            _originalProgressId = Game.ProgressId;

            Name = Game.Name ?? string.Empty;
            Publisher = Game.Publisher ?? string.Empty;
            Developer = Game.Developer ?? string.Empty;
            Excited = Game.Excited;
            FinishedIn = Game.FinishedIn ?? string.Empty;
            OnPropertyChanged(nameof(DisplayFinishHistory));
            AvgPlaytime = Game.AvgPlayTime;
            MetaScore = Game.MetaScore;
            SlugName = Game.SlugName ?? string.Empty;
            ExeName = Game.ExeName ?? string.Empty;
            InitExeName = Game.InitExeName ?? string.Empty;
            SaveFolderPath = Game.SaveFolderPath ?? string.Empty;
            BackupFolderPath = Game.BackupFolderPath ?? string.Empty;
            ImagePath = Game.ImagePath ?? string.Empty;
            GImagePath = Game.GImagePath ?? string.Empty;
            SteamLibraryImagePath = Game.SteamLibraryImagePath ?? string.Empty;
            Score = Game.Score;
            SyncPublishDate(Game.PublishDate);
            UpdatePlayTimeFieldsFromSeconds();
            HasInitExeName = !string.IsNullOrWhiteSpace(Game.InitExeName);

            var ownershipInfo = Game.OwnershipInfo;
            SteamAccount = ownershipInfo?.SteamAccount ?? string.Empty;
            SteamPassword = ownershipInfo?.SteamPassword ?? string.Empty;
            UbisoftAccount = ownershipInfo?.UbisoftAccount ?? string.Empty;
            UbisoftPassword = ownershipInfo?.UbisoftPassword ?? string.Empty;
            SupportDocumentUrl = ownershipInfo?.SupportDocumentUrl ?? string.Empty;
            DownloadUrl = ownershipInfo?.DownloadUrl ?? string.Empty;
            OwnershipNote = ownershipInfo?.Note ?? string.Empty;
            IsOwnershipInfoVisible = false;
        }

        private GameOwnershipInfo? CreateOwnershipInfo()
        {
            var values = new[]
            {
                SteamAccount,
                SteamPassword,
                UbisoftAccount,
                UbisoftPassword,
                SupportDocumentUrl,
                DownloadUrl,
                OwnershipNote
            };

            if (values.All(string.IsNullOrWhiteSpace))
                return null;

            return new GameOwnershipInfo
            {
                GameId = Game.Id,
                SteamAccount = SteamAccount.Trim(),
                SteamPassword = SteamPassword.Trim(),
                UbisoftAccount = UbisoftAccount.Trim(),
                UbisoftPassword = UbisoftPassword.Trim(),
                SupportDocumentUrl = SupportDocumentUrl.Trim(),
                DownloadUrl = DownloadUrl.Trim(),
                Note = OwnershipNote.Trim()
            };
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
            RefreshViewState();
        }

        private void RefreshViewState()
        {
            foreach (var propertyName in new[]
            {
                nameof(DisplayName), nameof(DisplayPublishDate), nameof(DisplayPublisher), nameof(DisplayDeveloper),
                nameof(DisplaySeries), nameof(DisplayGenres), nameof(DisplayThemes), nameof(DisplayMetaScore),
                nameof(DisplayExcited), nameof(DisplayAvgPlayTime), nameof(DisplayFinishedIn), nameof(DisplayFinishHistory), nameof(DisplayProcessName),
                nameof(DisplayInitProcessName), nameof(DisplaySaveFolderPath), nameof(DisplayBackupFolderPath),
                nameof(DisplayOwnership), nameof(DisplayProgress), nameof(DisplayScore), nameof(DisplayPlayTime),
                nameof(HasSlugName), nameof(HasInitExeNameForView), nameof(HasScore), nameof(HasSpecialOwnership)
            })
            {
                OnPropertyChanged(propertyName);
            }
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

        private const string DlcNameTag = " [DLC]";

        private static string EnsureDlcNameTag(string name)
        {
            var trimmedName = name?.Trim() ?? string.Empty;
            return trimmedName.EndsWith(DlcNameTag, StringComparison.OrdinalIgnoreCase)
                ? trimmedName
                : trimmedName + DlcNameTag;
        }

        private static string RemoveDlcNameTag(string name)
        {
            var trimmedName = name?.Trim() ?? string.Empty;
            return trimmedName.EndsWith(DlcNameTag, StringComparison.OrdinalIgnoreCase)
                ? trimmedName[..^DlcNameTag.Length].TrimEnd()
                : trimmedName;
        }

        private void ClearDlcRuntimeData()
        {
            ExeName = string.Empty;
            InitExeName = string.Empty;
            HasInitExeName = false;
            SaveFolderPath = string.Empty;
            BackupFolderPath = string.Empty;
            PlayTimeHours = 0;
            PlayTimeMinutes = 0;
            AdjustMinutes = 0;
            Game.TotalPlayTimeSeconds = 0;
            Game.Ready = false;
            Game.GamePosition = null;
        }

        partial void OnNameChanged(string value) => Game.Name = value;
        partial void OnIsDlcChanged(bool value)
        {
            Game.IsDlc = value;
            if (value)
            {
                Name = EnsureDlcNameTag(Name);
                ClearDlcRuntimeData();
            }
            else
            {
                Game.BaseGameId = null;
                SelectedBaseGame = null;
                Name = RemoveDlcNameTag(Name);
            }
        }

        partial void OnSelectedBaseGameChanged(Game? value)
        {
            if (!IsDlc)
                return;

            Game.BaseGameId = value?.Id;
            Game.SeriesId = value?.SeriesId;
            SelectedSeries = AllSeries.FirstOrDefault(series => series.Id == value?.SeriesId);
        }
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
        partial void OnSteamLibraryImagePathChanged(string value) => Game.SteamLibraryImagePath = value;

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

        partial void OnSelectedOwnershipChanged(OwnershipStatus? value)
        {
            if (!HasSpecialOwnership)
                IsOwnershipInfoVisible = false;
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsAnyPopupOpen))]
        private bool isFinishPopupOpen;

        [ObservableProperty]
        private string finishScoreInput = string.Empty;
    }
}
