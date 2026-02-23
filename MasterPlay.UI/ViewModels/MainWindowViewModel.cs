using AngleSharp.Io;
using CommunityToolkit.Mvvm.ComponentModel;
using MasterPlay.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;

namespace MasterPlay.UI.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        #region Constants

        // Change default items per page here
        private const int DefaultPageSize = 30;

        #endregion

        #region Fields

        private string _searchText = string.Empty;
        private string _viewMode = "All";
        private string _searchBy = "All";
        private int _sortIndex = 0;

        private int _currentPage = 1;
        private int _pageSize = DefaultPageSize;
        private int _totalItems = 0;

        private List<GameViewModel> _filteredGamesCache = new();

        #endregion

        #region Constructor

        public MainWindowViewModel()
        {
            Games = new ObservableCollection<GameViewModel>();
        }

        #endregion

        #region Collection Properties

        public ObservableCollection<GameViewModel> Games { get; }

        #endregion

        #region Filter Properties

        public string SearchText
        {
            get => _searchText;
            set => SetProperty(ref _searchText, value ?? string.Empty);
        }

        public string ViewMode
        {
            get => _viewMode;
            private set => SetProperty(ref _viewMode, value ?? "All");
        }

        public string SearchBy
        {
            get => _searchBy;
            private set => SetProperty(ref _searchBy, value ?? "All");
        }

        public int SortIndex
        {
            get => _sortIndex;
            private set => SetProperty(ref _sortIndex, value);
        }

        #endregion

        #region Pagination Properties

        public int CurrentPage => _currentPage;

        public int PageSize => _pageSize;

        public int TotalItems => _totalItems;

        public int TotalPages => Math.Max(1, (int)Math.Ceiling(_totalItems / (double)_pageSize));

        public bool CanGoPrev => _currentPage > 1;

        public bool CanGoNext => _currentPage < TotalPages;

        public string PageInfoText => $"Page {CurrentPage} / {TotalPages}";

        public string PaginationSummaryText
        {
            get
            {
                if (_totalItems <= 0)
                {
                    return "Showing 0 items";
                }

                int start = ((_currentPage - 1) * _pageSize) + 1;
                int end = Math.Min(_currentPage * _pageSize, _totalItems);

                return $"Showing {start}-{end} of {_totalItems}";
            }
        }

        #endregion

        #region Public Data Methods

        public void LoadGamesData()
        {
            DataService.SortGames();
            DataService.SyncReadyGames();
            ApplyFilterAndSort(resetToFirstPage: false);
        }

        public void ApplyFilterAndSort(bool resetToFirstPage = true)
        {
            if (resetToFirstPage)
            {
                _currentPage = 1;
            }

            var allData = DataService.LoadGames()
                .Where(g => !g.IsDisabled)
                .Select(g => new GameViewModel(g))
                .AsQueryable();

            allData = ApplyViewModeFilter(allData);
            allData = ApplySearchFilter(allData);
            allData = ApplySorting(allData);

            _filteredGamesCache = allData.ToList();
            _totalItems = _filteredGamesCache.Count;

            RefreshCurrentPageItems();
        }

        public void RefreshCurrentPageItems()
        {
            ClampCurrentPage();

            int skip = (_currentPage - 1) * _pageSize;

            var pageItems = _filteredGamesCache
                .Skip(skip)
                .Take(_pageSize)
                .ToList();

            Games.Clear();
            foreach (var item in pageItems)
            {
                Games.Add(item);
            }

            NotifyPaginationStateChanged();
        }

        #endregion

        #region Public State Methods

        public void ResetUIState()
        {
            _searchText = string.Empty;
            _viewMode = "All";
            _searchBy = "All";
            _sortIndex = 0;
            _currentPage = 1;

            OnPropertyChanged(nameof(SearchText));
            OnPropertyChanged(nameof(ViewMode));
            OnPropertyChanged(nameof(SearchBy));
            OnPropertyChanged(nameof(SortIndex));

            NotifyPaginationStateChanged();
        }

        public void SetSearchText(string searchText, bool applyImmediately = true)
        {
            SearchText = searchText ?? string.Empty;

            if (applyImmediately)
            {
                ApplyFilterAndSort(resetToFirstPage: true);
            }
        }

        public void SetViewMode(string viewMode)
        {
            ViewMode = string.IsNullOrWhiteSpace(viewMode) ? "All" : viewMode;
            ApplyFilterAndSort(resetToFirstPage: true);
        }

        public void SetSearchBy(string searchBy)
        {
            SearchBy = string.IsNullOrWhiteSpace(searchBy) ? "All" : searchBy;

            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                ApplyFilterAndSort(resetToFirstPage: true);
            }
        }

        public void SetSortIndex(int sortIndex)
        {
            if (sortIndex < 0)
            {
                sortIndex = 0;
            }

            SortIndex = sortIndex;
            ApplyFilterAndSort(resetToFirstPage: true);
        }

        public void SetPageSize(int pageSize)
        {
            if (pageSize <= 0 || _pageSize == pageSize)
            {
                return;
            }

            _pageSize = pageSize;
            _currentPage = 1;

            OnPropertyChanged(nameof(PageSize));
            RefreshCurrentPageItems();
        }

        #endregion

        #region Public Pagination Methods

        public void GoToPreviousPage()
        {
            if (!CanGoPrev)
            {
                return;
            }

            _currentPage--;
            RefreshCurrentPageItems();
        }

        public void GoToNextPage()
        {
            if (!CanGoNext)
            {
                return;
            }

            _currentPage++;
            RefreshCurrentPageItems();
        }

        public void GoToPage(int page)
        {
            _currentPage = page;
            RefreshCurrentPageItems();
        }

        #endregion

        #region Private Filter Methods

        private IQueryable<GameViewModel> ApplyViewModeFilter(IQueryable<GameViewModel> source)
        {
            return _viewMode switch
            {
                "Plan" => source.Where(vm => !string.Equals(vm.Game.Progress, "Done", StringComparison.OrdinalIgnoreCase)),
                "Finish" => source.Where(vm => string.Equals(vm.Game.Progress, "Done", StringComparison.OrdinalIgnoreCase)),
                _ => source
            };
        }

        private IQueryable<GameViewModel> ApplySearchFilter(IQueryable<GameViewModel> source)
        {
            if (string.IsNullOrWhiteSpace(_searchText))
            {
                return source;
            }

            string s = _searchText.Trim().ToLowerInvariant();

            return _searchBy switch
            {
                "Name" => source.Where(vm => (vm.Game.Name ?? string.Empty).ToLower().Contains(s)),

                "Series" => source.Where(vm => (vm.Game.Series ?? string.Empty).ToLower().Contains(s)),

                "Publisher" => source.Where(vm => (vm.Game.Publisher ?? string.Empty).ToLower().Contains(s)),

                "Developer" => source.Where(vm => (vm.Game.Developer ?? string.Empty).ToLower().Contains(s)),

                "Release" => ApplyReleaseYearSearch(source, s),

                _ => source.Where(vm =>
                    (vm.Game.Name ?? string.Empty).ToLower().Contains(s) ||
                    (vm.Game.Series ?? string.Empty).ToLower().Contains(s))
            };
        }

        private IQueryable<GameViewModel> ApplyReleaseYearSearch(IQueryable<GameViewModel> source, string searchValue)
        {
            if (!int.TryParse(searchValue, out int year))
            {
                return source.Where(_ => false);
            }

            return source.Where(vm => vm.Game.PublishDate.HasValue && vm.Game.PublishDate.Value.Year == year);
        }

        private IQueryable<GameViewModel> ApplySorting(IQueryable<GameViewModel> source)
        {
            return _sortIndex switch
            {
                0 => source.OrderByDescending(vm => vm.Game.Ready)
                           .ThenByDescending(vm => vm.Game.PublishDate),

                1 => source.OrderByDescending(vm => vm.Game.Ready)
                           .ThenByDescending(vm => vm.Game.PublishDate)
                           .ThenBy(vm => vm.Game.Name),

                2 => source.OrderBy(vm => vm.Game.Name)
                           .ThenBy(vm => vm.Game.Ready)
                           .ThenBy(vm => vm.Game.TotalPlayTimeSeconds)
                           .ThenByDescending(vm => vm.Game.PublishDate),

                3 => source.OrderByDescending(vm => vm.Game.TotalPlayTimeSeconds)
                           .ThenBy(vm => vm.Game.Ready)
                           .ThenBy(vm => vm.Game.Name)
                           .ThenByDescending(vm => vm.Game.PublishDate),

                4 => source.OrderByDescending(vm => vm.Game.PublishDate)
                           .ThenBy(vm => vm.Game.Name)
                           .ThenBy(vm => vm.Game.Ready)
                           .ThenBy(vm => vm.Game.TotalPlayTimeSeconds),

                5 => source.OrderByDescending(vm => vm.Game.MetaScore)
                           .ThenBy(vm => vm.Game.Name)
                           .ThenByDescending(vm => vm.Game.PublishDate),

                _ => source.OrderByDescending(vm => vm.Game.Ready)
                           .ThenBy(vm => vm.Game.Name)
                           .ThenBy(vm => vm.Game.TotalPlayTimeSeconds)
                           .ThenBy(vm => vm.Game.PublishDate)
            };
        }

        #endregion

        #region Private Pagination Helpers

        private void ClampCurrentPage()
        {
            if (_currentPage < 1)
            {
                _currentPage = 1;
            }

            int totalPages = TotalPages;
            if (_currentPage > totalPages)
            {
                _currentPage = totalPages;
            }
        }

        private void NotifyPaginationStateChanged()
        {
            OnPropertyChanged(nameof(CurrentPage));
            OnPropertyChanged(nameof(PageSize));
            OnPropertyChanged(nameof(TotalItems));
            OnPropertyChanged(nameof(TotalPages));
            OnPropertyChanged(nameof(CanGoPrev));
            OnPropertyChanged(nameof(CanGoNext));
            OnPropertyChanged(nameof(PageInfoText));
            OnPropertyChanged(nameof(PaginationSummaryText));
        }
        #endregion
 
    }
}