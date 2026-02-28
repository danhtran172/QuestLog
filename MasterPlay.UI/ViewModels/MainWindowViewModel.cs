using AngleSharp.Io;
using CommunityToolkit.Mvvm.ComponentModel;
using MasterPlay.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace MasterPlay.UI.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        #region Constants

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

        #region Mode Properties

        public bool IsAllMode => string.Equals(_viewMode, "All", StringComparison.OrdinalIgnoreCase);

        public bool IsPlanMode => string.Equals(_viewMode, "Plan", StringComparison.OrdinalIgnoreCase);

        public bool IsFinishMode => string.Equals(_viewMode, "Finish", StringComparison.OrdinalIgnoreCase);
        public bool IsPlayQueueMode => string.Equals(_viewMode, "PlayQueue", StringComparison.OrdinalIgnoreCase);

        #endregion

        #region Additional Info Properties

        public bool ShouldShowSeries => SearchBy == "Series";

        public bool ShouldShowDeveloper => SearchBy == "Developer";

        public bool ShouldShowPublisher => SearchBy == "Publisher" && !IsAllMode;

        public bool ShouldShowPlaytime => SortIndex == 3 && !IsAllMode;

        public bool ShouldShowExcited => SortIndex == 6 && !IsPlanMode;

        public bool ShouldShowScore => SortIndex == 7 && !IsFinishMode;

        public bool ShouldShowOwnership => SortIndex == 8 && !IsPlanMode;

        // AvgPlaytime: chỉ hiển thị khi sort theo avg playtime và không phải Finish mode (vì Finish đã có sẵn)
        public bool ShouldShowAvgPlaytime => SortIndex == 10 && !IsFinishMode;

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
            private set
            {
                if (SetProperty(ref _viewMode, value ?? "All"))
                {
                    OnPropertyChanged(nameof(IsAllMode));
                    OnPropertyChanged(nameof(IsPlanMode));
                    OnPropertyChanged(nameof(IsFinishMode));
                    OnPropertyChanged(nameof(IsPlayQueueMode));
                    OnPropertyChanged(nameof(ShouldShowPublisher));
                    OnPropertyChanged(nameof(ShouldShowPlaytime));
                    OnPropertyChanged(nameof(ShouldShowExcited));
                    OnPropertyChanged(nameof(ShouldShowScore));
                    OnPropertyChanged(nameof(ShouldShowOwnership));
                    OnPropertyChanged(nameof(ShouldShowAvgPlaytime));
                }
            }
        }

        public string SearchBy
        {
            get => _searchBy;
            private set
            {
                if (SetProperty(ref _searchBy, value ?? "All"))
                {
                    OnPropertyChanged(nameof(ShouldShowSeries));
                    OnPropertyChanged(nameof(ShouldShowDeveloper));
                    OnPropertyChanged(nameof(ShouldShowPublisher));
                }
            }
        }

        public int SortIndex
        {
            get => _sortIndex;
            private set
            {
                if (SetProperty(ref _sortIndex, value))
                {
                    OnPropertyChanged(nameof(ShouldShowPlaytime));
                    OnPropertyChanged(nameof(ShouldShowExcited));
                    OnPropertyChanged(nameof(ShouldShowScore));
                    OnPropertyChanged(nameof(ShouldShowOwnership));
                    OnPropertyChanged(nameof(ShouldShowAvgPlaytime));
                }
            }
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
            OnPropertyChanged(nameof(IsAllMode));
            OnPropertyChanged(nameof(IsPlanMode));
            OnPropertyChanged(nameof(IsFinishMode));
            OnPropertyChanged(nameof(ShouldShowSeries));
            OnPropertyChanged(nameof(ShouldShowDeveloper));
            OnPropertyChanged(nameof(ShouldShowPublisher));
            OnPropertyChanged(nameof(ShouldShowPlaytime));
            OnPropertyChanged(nameof(ShouldShowExcited));
            OnPropertyChanged(nameof(ShouldShowScore));
            OnPropertyChanged(nameof(ShouldShowOwnership));
            OnPropertyChanged(nameof(ShouldShowAvgPlaytime));

            NotifyPaginationStateChanged();
        }

        public void SetSearchText(string searchText, bool applyImmediately = true)
        {
            SearchText = searchText ?? string.Empty;

            if (applyImmediately)
            {
                ApplyFilterAndSort(true);
            }
        }

        public void SetViewMode(string viewMode)
        {
            ViewMode = string.IsNullOrWhiteSpace(viewMode) ? "All" : viewMode;
            ApplyFilterAndSort(true);
        }

        public void SetSearchBy(string searchBy)
        {
            SearchBy = string.IsNullOrWhiteSpace(searchBy) ? "All" : searchBy;

            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                ApplyFilterAndSort(true);
            }
        }

        public void SetSortIndex(int sortIndex)
        {
            if (sortIndex < 0)
            {
                sortIndex = 0;
            }

            SortIndex = sortIndex;
            ApplyFilterAndSort(true);
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
                return;

            _currentPage--;
            RefreshCurrentPageItems();
        }

        public void GoToNextPage()
        {
            if (!CanGoNext)
                return;

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
                "PlayQueue" => source.Where(vm => !string.Equals(vm.Game.Progress, "Done", StringComparison.OrdinalIgnoreCase)),
                _ => source
            };
        }

        private IQueryable<GameViewModel> ApplySearchFilter(IQueryable<GameViewModel> source)
        {
            if (string.IsNullOrWhiteSpace(_searchText))
                return source;

            string s = _searchText.Trim().ToLowerInvariant();

            return _searchBy switch
            {
                "Name" => source.Where(vm =>
                    (vm.Game.Name ?? "").ToLower().Contains(s) ||
                    MatchesInitials(vm.Game.Name ?? "", s)),

                "Series" => source.Where(vm =>
                    (vm.Game.Series ?? "").ToLower().Contains(s) ||
                    MatchesInitials(vm.Game.Series ?? "", s)),

                "Publisher" => source.Where(vm =>
                    (vm.Game.Publisher ?? "").ToLower().Contains(s) ||
                    MatchesInitials(vm.Game.Publisher ?? "", s)),

                "Developer" => source.Where(vm =>
                    (vm.Game.Developer ?? "").ToLower().Contains(s) ||
                    MatchesInitials(vm.Game.Developer ?? "", s)),

                "Release" => ApplyReleaseYearSearch(source, s),

                _ => source.Where(vm =>
                    (vm.Game.Name ?? "").ToLower().Contains(s) ||
                    (vm.Game.Series ?? "").ToLower().Contains(s) ||
                    MatchesInitials(vm.Game.Name ?? "", s) ||
                    MatchesInitials(vm.Game.Series ?? "", s))
            };
        }

        private IQueryable<GameViewModel> ApplyReleaseYearSearch(IQueryable<GameViewModel> source, string searchValue)
        {
            if (!int.TryParse(searchValue, out int year))
                return source.Where(_ => false);

            return source.Where(vm => vm.Game.PublishDate.HasValue &&
                                      vm.Game.PublishDate.Value.Year == year);
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

                2 => source.OrderBy(vm => vm.Game.Name),

                3 => source.OrderByDescending(vm => vm.Game.TotalPlayTimeSeconds),

                4 => source.OrderByDescending(vm => vm.Game.PublishDate),

                5 => source.OrderByDescending(vm => vm.Game.MetaScore),

                6 => source.OrderByDescending(vm => vm.Game.Excited),

                7 => source.OrderByDescending(vm => vm.Game.Score),

                8 => source.OrderBy(vm => vm.Game.Ownership),

                9 => source.OrderBy(vm => vm.Game.Progress),

                10 => source.OrderByDescending(vm => vm.Game.AvgPlayTime),

                _ => source.OrderByDescending(vm => vm.Game.Ready)
            };
        }

        #endregion

        #region Private Helper Methods

        private bool MatchesInitials(string text, string search)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(search))
                return false;

            var words = text.Split(new[] { ' ', '-', '_', '.' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
                return false;

            var initials = new string(words.Select(w => char.ToLowerInvariant(w[0])).ToArray());
            return initials.Contains(search);
        }

        #endregion

        #region Private Pagination Helpers

        private void ClampCurrentPage()
        {
            if (_currentPage < 1)
                _currentPage = 1;

            if (_currentPage > TotalPages)
                _currentPage = TotalPages;
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