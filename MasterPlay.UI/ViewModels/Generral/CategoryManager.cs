using CommunityToolkit.Mvvm.Input;
using MasterPlay.Models;
using MasterPlay.Models.Models;
using MasterPlay.Models.Repositories;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace MasterPlay.UI.ViewModels
{
    public class CategoryManager : INotifyPropertyChanged
    {
        // Fields
        private ObservableCollection<Series> _allSeries;
        private ObservableCollection<Genre> _allGenres;
        private ObservableCollection<Theme> _allThemes;

        private ObservableCollection<Series> _filteredSeries;
        private ObservableCollection<Genre> _filteredGenres;
        private ObservableCollection<Theme> _filteredThemes;

        private string _seriesSearchText;
        private string _genresSearchText;
        private string _themesSearchText;

        private int? _editingSeriesId;
        private string _editingSeriesName;
        private int? _editingGenreId;
        private string _editingGenreName;
        private int? _editingThemeId;
        private string _editingThemeName;

        public event PropertyChangedEventHandler PropertyChanged;

        // Constructors
        public CategoryManager()
        {
            LoadData();
            InitializeCommands();
        }

        // Properties
        public ObservableCollection<Series> AllSeries
        {
            get => _allSeries;
            private set
            {
                _allSeries = value;
                OnPropertyChanged();
                FilterSeries();
            }
        }

        public ObservableCollection<Genre> AllGenres
        {
            get => _allGenres;
            private set
            {
                _allGenres = value;
                OnPropertyChanged();
                FilterGenres();
            }
        }

        public ObservableCollection<Theme> AllThemes
        {
            get => _allThemes;
            private set
            {
                _allThemes = value;
                OnPropertyChanged();
                FilterThemes();
            }
        }

        public ObservableCollection<Series> FilteredSeries
        {
            get => _filteredSeries;
            private set
            {
                _filteredSeries = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<Genre> FilteredGenres
        {
            get => _filteredGenres;
            private set
            {
                _filteredGenres = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<Theme> FilteredThemes
        {
            get => _filteredThemes;
            private set
            {
                _filteredThemes = value;
                OnPropertyChanged();
            }
        }

        public string SeriesSearchText
        {
            get => _seriesSearchText;
            set
            {
                _seriesSearchText = value;
                OnPropertyChanged();
                FilterSeries();
                OnPropertyChanged(nameof(CreateNewSeriesText));
            }
        }

        public string GenresSearchText
        {
            get => _genresSearchText;
            set
            {
                _genresSearchText = value;
                OnPropertyChanged();
                FilterGenres();
                OnPropertyChanged(nameof(CreateNewGenreText));
            }
        }

        public string ThemesSearchText
        {
            get => _themesSearchText;
            set
            {
                _themesSearchText = value;
                OnPropertyChanged();
                FilterThemes();
                OnPropertyChanged(nameof(CreateNewThemeText));
            }
        }

        public int? EditingSeriesId
        {
            get => _editingSeriesId;
            set
            {
                _editingSeriesId = value;
                OnPropertyChanged();
            }
        }

        public string EditingSeriesName
        {
            get => _editingSeriesName;
            set
            {
                _editingSeriesName = value;
                OnPropertyChanged();
            }
        }

        public int? EditingGenreId
        {
            get => _editingGenreId;
            set
            {
                _editingGenreId = value;
                OnPropertyChanged();
            }
        }

        public string EditingGenreName
        {
            get => _editingGenreName;
            set
            {
                _editingGenreName = value;
                OnPropertyChanged();
            }
        }

        public int? EditingThemeId
        {
            get => _editingThemeId;
            set
            {
                _editingThemeId = value;
                OnPropertyChanged();
            }
        }

        public string EditingThemeName
        {
            get => _editingThemeName;
            set
            {
                _editingThemeName = value;
                OnPropertyChanged();
            }
        }

        public string CreateNewSeriesText => string.IsNullOrWhiteSpace(SeriesSearchText) ? "" : $"Create \"{SeriesSearchText}\"";
        public string CreateNewGenreText => string.IsNullOrWhiteSpace(GenresSearchText) ? "" : $"Create \"{GenresSearchText}\"";
        public string CreateNewThemeText => string.IsNullOrWhiteSpace(ThemesSearchText) ? "" : $"Create \"{ThemesSearchText}\"";

        // Commands
        public ICommand CreateSeriesCommand { get; private set; }
        public ICommand CreateGenreCommand { get; private set; }
        public ICommand CreateThemeCommand { get; private set; }
        public ICommand EditSeriesCommand { get; private set; }
        public ICommand SaveSeriesCommand { get; private set; }
        public ICommand DeleteSeriesCommand { get; private set; }
        public ICommand EditGenreCommand { get; private set; }
        public ICommand SaveGenreCommand { get; private set; }
        public ICommand DeleteGenreCommand { get; private set; }
        public ICommand EditThemeCommand { get; private set; }
        public ICommand SaveThemeCommand { get; private set; }
        public ICommand DeleteThemeCommand { get; private set; }

        // Private Methods
        private void InitializeCommands()
        {
            CreateSeriesCommand = new RelayCommand(CreateSeries);
            CreateGenreCommand = new RelayCommand(CreateGenre);
            CreateThemeCommand = new RelayCommand(CreateTheme);
            EditSeriesCommand = new RelayCommand<Series>(OnEditSeries);
            SaveSeriesCommand = new RelayCommand<Series>(OnSaveSeries);
            DeleteSeriesCommand = new RelayCommand<Series>(OnDeleteSeries);
            EditGenreCommand = new RelayCommand<Genre>(OnEditGenre);
            SaveGenreCommand = new RelayCommand<Genre>(OnSaveGenre);
            DeleteGenreCommand = new RelayCommand<Genre>(OnDeleteGenre);
            EditThemeCommand = new RelayCommand<Theme>(OnEditTheme);
            SaveThemeCommand = new RelayCommand<Theme>(OnSaveTheme);
            DeleteThemeCommand = new RelayCommand<Theme>(OnDeleteTheme);
        }

        private void LoadData()
        {
            using var uow = new UnitOfWork(new MasterPlayContext());
            AllSeries = new ObservableCollection<Series>(uow.Series.GetAll().OrderBy(s => s.Name));
            AllGenres = new ObservableCollection<Genre>(uow.Genres.GetAll().OrderBy(g => g.Name));
            AllThemes = new ObservableCollection<Theme>(uow.Themes.GetAll().OrderBy(t => t.Name));
        }

        private void FilterSeries()
        {
            if (string.IsNullOrWhiteSpace(SeriesSearchText))
                FilteredSeries = new ObservableCollection<Series>(AllSeries);
            else
                FilteredSeries = new ObservableCollection<Series>(
                    AllSeries.Where(s => s.Name.IndexOf(SeriesSearchText, StringComparison.OrdinalIgnoreCase) >= 0)
                );
        }

        private void FilterGenres()
        {
            if (string.IsNullOrWhiteSpace(GenresSearchText))
                FilteredGenres = new ObservableCollection<Genre>(AllGenres);
            else
                FilteredGenres = new ObservableCollection<Genre>(
                    AllGenres.Where(g => g.Name.IndexOf(GenresSearchText, StringComparison.OrdinalIgnoreCase) >= 0)
                );
        }

        private void FilterThemes()
        {
            if (string.IsNullOrWhiteSpace(ThemesSearchText))
                FilteredThemes = new ObservableCollection<Theme>(AllThemes);
            else
                FilteredThemes = new ObservableCollection<Theme>(
                    AllThemes.Where(t => t.Name.IndexOf(ThemesSearchText, StringComparison.OrdinalIgnoreCase) >= 0)
                );
        }

        private void CreateSeries()
        {
            if (string.IsNullOrWhiteSpace(SeriesSearchText)) return;

            using var uow = new UnitOfWork(new MasterPlayContext());
            var existing = uow.Series.Find(s => s.Name.ToLower() == SeriesSearchText.ToLower()).FirstOrDefault();
            if (existing != null)
            {
                MessageBox.Show("Series already exists.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var newSeries = new Series { Name = SeriesSearchText };
            uow.Series.Add(newSeries);
            uow.Complete();

            AllSeries.Add(newSeries);
            AllSeries = new ObservableCollection<Series>(AllSeries.OrderBy(s => s.Name));
            SeriesSearchText = string.Empty;
        }

        private void CreateGenre()
        {
            if (string.IsNullOrWhiteSpace(GenresSearchText)) return;

            using var uow = new UnitOfWork(new MasterPlayContext());
            var existing = uow.Genres.Find(g => g.Name.ToLower() == GenresSearchText.ToLower()).FirstOrDefault();
            if (existing != null)
            {
                MessageBox.Show("Genre already exists.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var newGenre = new Genre { Name = GenresSearchText };
            uow.Genres.Add(newGenre);
            uow.Complete();

            AllGenres.Add(newGenre);
            AllGenres = new ObservableCollection<Genre>(AllGenres.OrderBy(g => g.Name));
            GenresSearchText = string.Empty;
        }

        private void CreateTheme()
        {
            if (string.IsNullOrWhiteSpace(ThemesSearchText)) return;

            using var uow = new UnitOfWork(new MasterPlayContext());
            var existing = uow.Themes.Find(t => t.Name.ToLower() == ThemesSearchText.ToLower()).FirstOrDefault();
            if (existing != null)
            {
                MessageBox.Show("Theme already exists.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var newTheme = new Theme { Name = ThemesSearchText };
            uow.Themes.Add(newTheme);
            uow.Complete();

            AllThemes.Add(newTheme);
            AllThemes = new ObservableCollection<Theme>(AllThemes.OrderBy(t => t.Name));
            ThemesSearchText = string.Empty;
        }

        private void OnEditSeries(Series series)
        {
            EditingSeriesId = series.Id;
            EditingSeriesName = series.Name;
            EditingGenreId = null;
            EditingThemeId = null;
        }

        private void OnSaveSeries(Series series)
        {
            if (EditingSeriesId == null || EditingSeriesId != series.Id) return;

            if (string.IsNullOrWhiteSpace(EditingSeriesName))
            {
                MessageBox.Show("Series name cannot be empty.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            using var uow = new UnitOfWork(new MasterPlayContext());
            var seriesFromDb = uow.Series.GetById(series.Id);
            if (seriesFromDb != null)
            {
                seriesFromDb.Name = EditingSeriesName;
                uow.Complete();

                series.Name = EditingSeriesName; // update local item
                AllSeries = new ObservableCollection<Series>(AllSeries.OrderBy(s => s.Name));
            }
            EditingSeriesId = null;
        }

        private void OnDeleteSeries(Series series)
        {
            var result = MessageBox.Show($"Are you sure you want to delete '{series.Name}'? This will remove all associations with games.",
                                         "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            using var uow = new UnitOfWork(new MasterPlayContext());
            var seriesFromDb = uow.Series.GetById(series.Id);
            if (seriesFromDb != null)
            {
                uow.Series.Remove(seriesFromDb);
                uow.Complete();

                AllSeries.Remove(series);
            }
            EditingSeriesId = null;
        }

        private void OnEditGenre(Genre genre)
        {
            EditingGenreId = genre.Id;
            EditingGenreName = genre.Name;
            EditingSeriesId = null;
            EditingThemeId = null;
        }

        private void OnSaveGenre(Genre genre)
        {
            if (EditingGenreId == null || EditingGenreId != genre.Id) return;

            if (string.IsNullOrWhiteSpace(EditingGenreName))
            {
                MessageBox.Show("Genre name cannot be empty.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            using var uow = new UnitOfWork(new MasterPlayContext());
            var genreFromDb = uow.Genres.GetById(genre.Id);
            if (genreFromDb != null)
            {
                genreFromDb.Name = EditingGenreName;
                uow.Complete();

                genre.Name = EditingGenreName;
                AllGenres = new ObservableCollection<Genre>(AllGenres.OrderBy(g => g.Name));
            }
            EditingGenreId = null;
        }

        private void OnDeleteGenre(Genre genre)
        {
            var result = MessageBox.Show($"Are you sure you want to delete '{genre.Name}'? This will remove all associations with games.",
                                         "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            using var uow = new UnitOfWork(new MasterPlayContext());
            var genreFromDb = uow.Genres.GetById(genre.Id);
            if (genreFromDb != null)
            {
                // Xóa tất cả liên kết GameGenre trước
                var gameGenres = uow.GameGenres.Find(gg => gg.GenreId == genre.Id);
                uow.GameGenres.RemoveRange(gameGenres);

                uow.Genres.Remove(genreFromDb);
                uow.Complete();

                AllGenres.Remove(genre);
            }
            EditingGenreId = null;
        }

        private void OnEditTheme(Theme theme)
        {
            EditingThemeId = theme.Id;
            EditingThemeName = theme.Name;
            EditingSeriesId = null;
            EditingGenreId = null;
        }

        private void OnSaveTheme(Theme theme)
        {
            if (EditingThemeId == null || EditingThemeId != theme.Id) return;

            if (string.IsNullOrWhiteSpace(EditingThemeName))
            {
                MessageBox.Show("Theme name cannot be empty.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            using var uow = new UnitOfWork(new MasterPlayContext());
            var themeFromDb = uow.Themes.GetById(theme.Id);
            if (themeFromDb != null)
            {
                themeFromDb.Name = EditingThemeName;
                uow.Complete();

                theme.Name = EditingThemeName;
                AllThemes = new ObservableCollection<Theme>(AllThemes.OrderBy(t => t.Name));
            }
            EditingThemeId = null;
        }

        private void OnDeleteTheme(Theme theme)
        {
            var result = MessageBox.Show($"Are you sure you want to delete '{theme.Name}'? This will remove all associations with games.",
                                         "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            using var uow = new UnitOfWork(new MasterPlayContext());
            var themeFromDb = uow.Themes.GetById(theme.Id);
            if (themeFromDb != null)
            {
                // Xóa tất cả liên kết GameTheme trước
                var gameThemes = uow.GameThemes.Find(gt => gt.ThemeId == theme.Id);
                uow.GameThemes.RemoveRange(gameThemes);

                uow.Themes.Remove(themeFromDb);
                uow.Complete();

                AllThemes.Remove(theme);
            }
            EditingThemeId = null;
        }

        // Public Methods
        public void ResetEditing()
        {
            EditingSeriesId = null;
            EditingSeriesName = null;
            EditingGenreId = null;
            EditingGenreName = null;
            EditingThemeId = null;
            EditingThemeName = null;
        }

        // INotifyPropertyChanged
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}