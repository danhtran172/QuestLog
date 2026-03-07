using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GongSolutions.Wpf.DragDrop;
using MasterPlay.Models;
using MasterPlay.Models.Models;
using MasterPlay.UI.ViewModels.Generral;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace MasterPlay.UI.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject, IDropTarget
    {
        // Constants
        private const int DefaultPageSize = 30;
        private const int MaxPageSize = int.MaxValue;

        // Fields
        private string _searchText = string.Empty;
        private string _viewMode = "All";
        private string _searchBy = "All";
        private int _sortIndex = 0;

        private int _currentPage = 1;
        private int _pageSize = DefaultPageSize;
        private int _previousPageSize = DefaultPageSize;
        private int _totalItems = 0;

        private List<GameViewModel> _filteredGamesCache = new();
        private List<ProgressStatus> _progressStatuses;

        // Filter Options
        public FilterOptions CurrentFilter { get; } = new FilterOptions();

        // Include None flags for MetaScore and AvgPlay
        [ObservableProperty]
        private bool _includeMetaScoreNone;

        [ObservableProperty]
        private bool _includeAvgPlayNone;

        // Commands
        public ICommand ApplyFilterCommand { get; }
        public ICommand ClearFilterCommand { get; }

        // Reverse sort property
        [ObservableProperty]
        private bool isReverseSort;

        // Constructor
        public MainWindowViewModel()
        {
            Games = new ObservableCollection<GameViewModel>();
            _progressStatuses = DataService.LoadProgressList();

            ApplyFilterCommand = new RelayCommand(ApplyFilter);
            ClearFilterCommand = new RelayCommand(ClearFilter);
        }

        // Collection Properties
        public ObservableCollection<GameViewModel> Games { get; }

        // Mode Properties
        public bool IsAllMode => string.Equals(_viewMode, "All", StringComparison.OrdinalIgnoreCase);
        public bool IsPlanMode => string.Equals(_viewMode, "Plan", StringComparison.OrdinalIgnoreCase);
        public bool IsFinishMode => string.Equals(_viewMode, "Finish", StringComparison.OrdinalIgnoreCase);
        public bool IsPlayQueueMode => string.Equals(_viewMode, "PlayQueue", StringComparison.OrdinalIgnoreCase);

        // Additional Info Properties
        public bool ShouldShowSeries => SearchBy == "Series";
        public bool ShouldShowDeveloper => SearchBy == "Developer";
        public bool ShouldShowPublisher => SearchBy == "Publisher" && !IsAllMode;
        public bool ShouldShowPlaytime => SortIndex == 3 && !IsAllMode;
        public bool ShouldShowExcited => SortIndex == 6 && !IsPlanMode;
        public bool ShouldShowScore => SortIndex == 7 && !IsFinishMode;
        public bool ShouldShowOwnership => SortIndex == 8 && !IsPlanMode;
        public bool ShouldShowAvgPlaytime => SortIndex == 10 && !IsFinishMode;

        // Filter Properties
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
                    if (IsPlayQueueMode)
                    {
                        _previousPageSize = _pageSize;
                        _pageSize = MaxPageSize;
                    }
                    else if (_pageSize == MaxPageSize)
                    {
                        _pageSize = _previousPageSize;
                    }

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

        // Pagination Properties
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
                    return "Showing 0 items";

                int start = ((_currentPage - 1) * _pageSize) + 1;
                int end = Math.Min(_currentPage * _pageSize, _totalItems);
                return $"Showing {start}-{end} of {_totalItems}";
            }
        }

        // NEW: Property kiểm tra có filter nào đang active không
        public bool HasActiveFilter
        {
            get
            {
                return CurrentFilter.ValueFinish.HasValue
                    || CurrentFilter.ValueExcited.HasValue
                    || CurrentFilter.ValueScore.HasValue
                    || CurrentFilter.ValuePlayTime.HasValue
                    || CurrentFilter.SelectedAvgPlays.Count > 0
                    || CurrentFilter.SelectedProgressIds.Count > 0
                    || CurrentFilter.SelectedOwnershipIds.Count > 0
                    || CurrentFilter.SelectedMetaScores.Count > 0
                    || IncludeMetaScoreNone
                    || IncludeAvgPlayNone;
            }
        }

        // Public Data Methods
        public void LoadGamesData()
        {
            ApplyFilterAndSort(resetToFirstPage: false);
        }

        public void ApplyFilterAndSort(bool resetToFirstPage = true)
        {
            if (resetToFirstPage)
                _currentPage = 1;

            var allData = DataService.LoadGames()
                .Where(g => !g.IsDisabled)
                .Select(g => new GameViewModel(g))
                .AsQueryable();

            allData = ApplyViewModeFilter(allData);

            if (!IsPlayQueueMode)
            {
                allData = ApplySearchFilter(allData);
            }

            var afterSearch = allData.AsEnumerable();

            var filtered = IsPlayQueueMode ? afterSearch : ApplyFilterOptions(afterSearch);

            var filteredQuery = filtered.AsQueryable();
            filteredQuery = ApplySorting(filteredQuery);

            _filteredGamesCache = filteredQuery.ToList();

            if (isReverseSort)
            {
                _filteredGamesCache.Reverse();
            }

            if (IsPlayQueueMode)
            {
                ResetAndPersistPlayQueueOrder();
            }

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
                Games.Add(item);

            NotifyPaginationStateChanged();
        }

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
            // Không cho phép tìm kiếm trong PlayQueue mode
            if (IsPlayQueueMode)
                return;

            SearchText = searchText ?? string.Empty;
            if (applyImmediately && !IsPlayQueueMode)
                ApplyFilterAndSort(true);
        }

        public void SetViewMode(string viewMode)
        {
            ViewMode = string.IsNullOrWhiteSpace(viewMode) ? "All" : viewMode;
            if (!IsPlayQueueMode)
            {
                SortIndex = 0;
            }
            ApplyFilterAndSort(true);
        }

        public void SetSearchBy(string searchBy)
        {
            // Không cho phép thay đổi search by trong PlayQueue mode
            if (IsPlayQueueMode)
                return;

            SearchBy = string.IsNullOrWhiteSpace(searchBy) ? "All" : searchBy;
            if (!string.IsNullOrWhiteSpace(_searchText) && !IsPlayQueueMode)
                ApplyFilterAndSort(true);
        }

        public void SetSortIndex(int sortIndex)
        {
            if (IsPlayQueueMode)
                return;

            if (sortIndex < 0)
                sortIndex = 0;
            SortIndex = sortIndex;
            if (!IsPlayQueueMode)
                ApplyFilterAndSort(true);
        }

        public void SetPageSize(int pageSize)
        {
            if (pageSize <= 0 || _pageSize == pageSize)
                return;

            if (IsPlayQueueMode)
                return;

            _pageSize = pageSize;
            _currentPage = 1;
            OnPropertyChanged(nameof(PageSize));
            RefreshCurrentPageItems();
        }

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

        public void ClearFilter()
        {
            if (IsPlayQueueMode)
                return;

            CurrentFilter.OperatorFinish = "=";
            CurrentFilter.ValueFinish = null;
            CurrentFilter.OperatorExcited = "=";
            CurrentFilter.ValueExcited = null;
            CurrentFilter.OperatorScore = "=";
            CurrentFilter.ValueScore = null;
            CurrentFilter.OperatorPlayTime = "=";
            CurrentFilter.ValuePlayTime = null;
            CurrentFilter.SelectedAvgPlays.Clear();
            CurrentFilter.SelectedProgressIds.Clear();
            CurrentFilter.SelectedOwnershipIds.Clear();
            CurrentFilter.SelectedMetaScores.Clear();
            CurrentFilter.IsReverse = false;
            IncludeMetaScoreNone = false;
            IncludeAvgPlayNone = false;

            OnPropertyChanged(nameof(HasActiveFilter));
        }

        private IQueryable<GameViewModel> ApplyViewModeFilter(IQueryable<GameViewModel> source)
        {
            return _viewMode switch
            {
                "Plan" => source.Where(vm => vm.Game.ProgressId != 1),
                "Finish" => source.Where(vm => vm.Game.ProgressId == 1),
                "PlayQueue" => source.Where(vm => vm.Game.ProgressId == 2 || vm.Game.ProgressId == 3 || vm.Game.ProgressId == 4),
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
                    (vm.Game.Series == null ? "" : vm.Game.Series.Name).ToLower().Contains(s) ||
                    MatchesInitials(vm.Game.Series == null ? "" : vm.Game.Series.Name, s)),

                "Publisher" => source.Where(vm =>
                    (vm.Game.Publisher ?? "").ToLower().Contains(s) ||
                    MatchesInitials(vm.Game.Publisher ?? "", s)),

                "Developer" => source.Where(vm =>
                    (vm.Game.Developer ?? "").ToLower().Contains(s) ||
                    MatchesInitials(vm.Game.Developer ?? "", s)),

                "Release" => ApplyReleaseYearSearch(source, s),

                _ => source.Where(vm =>
                    (vm.Game.Name ?? "").ToLower().Contains(s) ||
                    (vm.Game.Series == null ? "" : vm.Game.Series.Name).ToLower().Contains(s) ||
                    vm.Game.GameGenres.Any(gg => (gg.Genre == null ? "" : gg.Genre.Name).ToLower().Contains(s)) ||
                    vm.Game.GameThemes.Any(gt => (gt.Theme == null ? "" : gt.Theme.Name).ToLower().Contains(s)) ||
                    MatchesInitials(vm.Game.Name ?? "", s) ||
                    MatchesInitials(vm.Game.Series == null ? "" : vm.Game.Series.Name, s))
            };
        }

        private IQueryable<GameViewModel> ApplyReleaseYearSearch(IQueryable<GameViewModel> source, string searchValue)
        {
            if (!int.TryParse(searchValue, out int year))
                return source.Where(_ => false);
            return source.Where(vm => vm.Game.PublishDate.HasValue && vm.Game.PublishDate.Value.Year == year);
        }

        private IQueryable<GameViewModel> ApplySorting(IQueryable<GameViewModel> source)
{
    if (IsPlayQueueMode)
    {
        return source
            .OrderBy(vm => vm.Game.GamePosition != null && vm.Game.GamePosition.PlayOrder.HasValue ? 0 : 1)
            .ThenBy(vm => vm.Game.GamePosition != null ? vm.Game.GamePosition.PlayOrder : int.MaxValue)
            .ThenByDescending(vm => vm.Game.Excited)
            .ThenBy(vm => vm.Game.AvgPlayTime ?? int.MaxValue);
    }

    if (_sortIndex == 0)
    {
        if (IsAllMode)
        {
            return source
                .OrderByDescending(vm => vm.Game.PublishDate)
                .ThenByDescending(vm => vm.Game.Excited)
                .ThenBy(vm => vm.Game.Name)
                .ThenBy(vm => vm.Game.ProgressId ?? int.MaxValue)
                .ThenBy(vm => vm.Game.OwnershipId ?? int.MaxValue);
        }
        if (IsPlanMode)
        {
            return source
                .OrderBy(vm => vm.Game.ProgressId ?? int.MaxValue)
                .ThenByDescending(vm => vm.Game.Excited)
                .ThenBy(vm => vm.Game.AvgPlayTime ?? int.MaxValue)
                .ThenBy(vm => vm.Game.Name);
        }
        if (IsFinishMode)
        {
            return source
                .OrderBy(vm => vm.Game.ProgressId ?? int.MaxValue)
                .ThenByDescending(vm => vm.Game.Score)
                .ThenByDescending(vm => ViewModelHelpers.GetFinishedYear(vm.Game.FinishedIn))
                .ThenByDescending(vm => vm.Game.PublishDate)
                .ThenByDescending(vm => vm.Game.MetaScore);
        }
        return source.OrderByDescending(vm => vm.Game.PublishDate);
    }

    IOrderedQueryable<GameViewModel> ordered = source.OrderBy(vm => 1);

    switch (_sortIndex)
    {
        case 1:
            ordered = source.OrderByDescending(vm => vm.Game.Ready).ThenByDescending(vm => vm.Game.PublishDate).ThenBy(vm => vm.Game.Name);
            break;
        case 2:
            ordered = source.OrderBy(vm => vm.Game.Name);
            break;
        case 3:
            ordered = source.OrderByDescending(vm => vm.Game.TotalPlayTimeSeconds);
            break;
        case 4:
            ordered = source.OrderByDescending(vm => vm.Game.PublishDate);
            break;
        case 5:
            ordered = source.OrderByDescending(vm => vm.Game.MetaScore);
            break;
        case 6:
            ordered = source.OrderByDescending(vm => vm.Game.Excited);
            break;
        case 7:
            ordered = source.OrderByDescending(vm => vm.Game.Score);
            break;
        case 8:
            ordered = source.OrderBy(vm => vm.Game.OwnershipId ?? int.MaxValue);
            break;
        case 9:
            ordered = source.OrderBy(vm => vm.Game.ProgressId ?? int.MaxValue);
            break;
        case 10:
            ordered = source.OrderByDescending(vm => vm.Game.AvgPlayTime);
            break;
        default:
            ordered = source.OrderByDescending(vm => vm.Game.Ready);
            break;
    }

    return ordered;
}

        private IEnumerable<GameViewModel> ApplyFilterOptions(IEnumerable<GameViewModel> source)
        {
            return source.Where(vm => MatchesFilter(vm));
        }

        // SỬA LẠI LOGIC FILTER: None = 0 hoặc null, kết hợp OR với các level
        private bool MatchesFilter(GameViewModel vm)
        {
            var game = vm.Game;
            var filter = CurrentFilter;

            // Kiểm tra nếu không có filter nào được chọn (bao gồm cả None)
            bool hasAnyFilter = filter.ValueFinish.HasValue
                || filter.ValueExcited.HasValue
                || filter.ValueScore.HasValue
                || filter.ValuePlayTime.HasValue
                || filter.SelectedAvgPlays.Count > 0
                || filter.SelectedProgressIds.Count > 0
                || filter.SelectedOwnershipIds.Count > 0
                || filter.SelectedMetaScores.Count > 0
                || IncludeMetaScoreNone
                || IncludeAvgPlayNone;

            if (!hasAnyFilter) return true;

            bool matches = true;

            // Finish
            if (filter.ValueFinish.HasValue)
                matches = matches && CompareFinishedIn(game.FinishedIn, filter.ValueFinish.Value, filter.OperatorFinish);

            // Excited
            if (filter.ValueExcited.HasValue)
                matches = matches && CompareDouble(game.Excited, filter.ValueExcited.Value, filter.OperatorExcited);

            // Score
            if (filter.ValueScore.HasValue)
                matches = matches && CompareDouble(game.Score, filter.ValueScore.Value, filter.OperatorScore);

            // PlayTime
            if (filter.ValuePlayTime.HasValue)
                matches = matches && CompareInt(game.TotalPlayTimeSeconds, filter.ValuePlayTime.Value, filter.OperatorPlayTime);

            // Avg Play: None = 0 hoặc null, kết hợp OR với các level
            bool avgPass = true;
            if (IncludeAvgPlayNone || filter.SelectedAvgPlays.Count > 0)
            {
                avgPass = false;
                if (IncludeAvgPlayNone && (game.AvgPlayTime == null || game.AvgPlayTime <= 0))
                    avgPass = true;
                if (!avgPass && filter.SelectedAvgPlays.Count > 0 && MatchesAvgPlay(game))
                    avgPass = true;
            }
            matches = matches && avgPass;

            // Progress
            if (filter.SelectedProgressIds.Count > 0)
                matches = matches && filter.SelectedProgressIds.Contains(game.ProgressId ?? 0);

            // Ownership
            if (filter.SelectedOwnershipIds.Count > 0)
                matches = matches && filter.SelectedOwnershipIds.Contains(game.OwnershipId ?? 0);

            // MetaScore: None = 0 hoặc null, kết hợp OR với các level
            bool metaPass = true;
            if (IncludeMetaScoreNone || filter.SelectedMetaScores.Count > 0)
            {
                metaPass = false;
                if (IncludeMetaScoreNone && (game.MetaScore == null || game.MetaScore <= 0))
                    metaPass = true;
                if (!metaPass && filter.SelectedMetaScores.Count > 0 && MatchesMetaScore(game))
                    metaPass = true;
            }
            matches = matches && metaPass;

            if (filter.IsReverse)
                matches = !matches;

            return matches;
        }

        private bool CompareFinishedIn(string finishedIn, double target, string op)
        {
            if (string.IsNullOrWhiteSpace(finishedIn))
                return false;

            if (!double.TryParse(finishedIn, out double value))
                return false;

            return CompareDouble(value, target, op);
        }

        private bool CompareDouble(double? value, double target, string op)
        {
            if (!value.HasValue) return false;
            double epsilon = 0.000001;
            switch (op)
            {
                case ">": return value.Value > target + epsilon;
                case ">=": return value.Value >= target - epsilon;
                case "=": return Math.Abs(value.Value - target) < epsilon;
                case "<=": return value.Value <= target + epsilon;
                case "<": return value.Value < target - epsilon;
                default: return false;
            }
        }

        private bool CompareInt(int? value, int target, string op)
        {
            if (!value.HasValue) return false;
            switch (op)
            {
                case ">": return value.Value > target;
                case ">=": return value.Value >= target;
                case "=": return value.Value == target;
                case "<=": return value.Value <= target;
                case "<": return value.Value < target;
                default: return false;
            }
        }

        private bool MatchesAvgPlay(Game game)
        {
            if (!game.AvgPlayTime.HasValue) return false;
            int avg = game.AvgPlayTime.Value;
            foreach (var level in CurrentFilter.SelectedAvgPlays)
            {
                switch (level)
                {
                    case AvgPlayLevel.VeryLong:
                        if (avg >= 80) return true;
                        break;
                    case AvgPlayLevel.Long:
                        if (avg >= 60 && avg < 80) return true;
                        break;
                    case AvgPlayLevel.Medium:
                        if (avg >= 35 && avg < 60) return true;
                        break;
                    case AvgPlayLevel.Short:
                        if (avg >= 20 && avg < 35) return true;
                        break;
                    case AvgPlayLevel.VeryShort:
                        if (avg < 20 && avg > 0) return true;
                        break;
                }
            }
            return false;
        }

        private bool MatchesMetaScore(Game game)
        {
            if (!game.MetaScore.HasValue) return false;
            int meta = game.MetaScore.Value;
            foreach (var level in CurrentFilter.SelectedMetaScores)
            {
                switch (level)
                {
                    case MetaScoreLevel.VeryHigh:
                        if (meta >= 90) return true;
                        break;
                    case MetaScoreLevel.High:
                        if (meta >= 80 && meta < 90) return true;
                        break;
                    case MetaScoreLevel.Medium:
                        if (meta >= 70 && meta < 80) return true;
                        break;
                    case MetaScoreLevel.Low:
                        if (meta >= 50 && meta < 70) return true;
                        break;
                    case MetaScoreLevel.VeryLow:
                        if (meta < 50 && meta > 0) return true;
                        break;
                }
            }
            return false;
        }

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

        private void ResetAndPersistPlayQueueOrder()
        {
            if (!IsPlayQueueMode || _filteredGamesCache.Count == 0)
                return;

            var withOrder = _filteredGamesCache
                .Where(vm => vm.Game.GamePosition != null && vm.Game.GamePosition.PlayOrder.HasValue)
                .OrderBy(vm => vm.Game.GamePosition.PlayOrder.Value)
                .ToList();

            var withoutOrder = _filteredGamesCache
                .Where(vm => vm.Game.GamePosition == null || !vm.Game.GamePosition.PlayOrder.HasValue)
                .OrderByDescending(vm => vm.Game.Excited)
                .ThenBy(vm => vm.Game.AvgPlayTime ?? int.MaxValue)
                .ToList();

            var reordered = withOrder.Concat(withoutOrder).ToList();

            for (int i = 0; i < reordered.Count; i++)
            {
                var vm = reordered[i];
                if (vm.Game.GamePosition == null)
                    vm.Game.GamePosition = new GamePosition { GameId = vm.Game.Id };
                vm.Game.GamePosition.PlayOrder = i + 1;
            }

            DataService.UpdateGamePositions(reordered.Select(vm => vm.Game).ToList());
            _filteredGamesCache = reordered;
        }

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

        // IDropTarget Implementation
        void IDropTarget.DragOver(IDropInfo dropInfo)
        {
            if (IsPlayQueueMode && dropInfo.Data is GameViewModel)
            {
                dropInfo.Effects = DragDropEffects.Move;
                dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
            }
            else
            {
                dropInfo.Effects = DragDropEffects.None;
            }
        }

        void IDropTarget.Drop(IDropInfo dropInfo)
        {
            if (!IsPlayQueueMode)
                return;

            var draggedItem = dropInfo.Data as GameViewModel;
            var targetItem = dropInfo.TargetItem as GameViewModel;

            if (draggedItem == null)
                return;

            int oldIndex = Games.IndexOf(draggedItem);
            int newIndex = targetItem != null ? Games.IndexOf(targetItem) : Games.Count - 1;

            if (oldIndex == -1 || newIndex == -1)
                return;

            if (oldIndex < newIndex)
                newIndex--;

            Games.RemoveAt(oldIndex);
            Games.Insert(newIndex, draggedItem);

            var reorderedGames = Games.Select(vm => vm.Game).ToList();

            const int playingId = 2;
            const int nextPlayId = 3;
            const int intendedId = 4;

            var playingStatus = _progressStatuses.FirstOrDefault(p => p.Id == playingId);
            var nextPlayStatus = _progressStatuses.FirstOrDefault(p => p.Id == nextPlayId);
            var intendedStatus = _progressStatuses.FirstOrDefault(p => p.Id == intendedId);

            var playingGames = reorderedGames.Where(g => g.ProgressId == playingId).ToList();
            var nonPlayingGames = reorderedGames.Where(g => g.ProgressId != playingId).ToList();

            int nextPlayCount = Math.Min(3, nonPlayingGames.Count);

            foreach (var game in nonPlayingGames)
            {
                game.ProgressId = intendedId;
                game.Progress = intendedStatus;
            }

            for (int i = 0; i < nextPlayCount; i++)
            {
                var game = nonPlayingGames[i];
                game.ProgressId = nextPlayId;
                game.Progress = nextPlayStatus;
            }

            foreach (var game in playingGames)
            {
                game.Progress = playingStatus;
            }

            var newGameViewModels = reorderedGames.Select(g => new GameViewModel(g)).ToList();

            Games.Clear();
            foreach (var vm in newGameViewModels)
                Games.Add(vm);

            _filteredGamesCache = newGameViewModels;

            DataService.UpdateQueueOrderAndProgress(reorderedGames);

            NotifyPaginationStateChanged();
        }

        private void ApplyFilter()
        {
            // Không cho phép áp dụng filter trong PlayQueue mode
            if (IsPlayQueueMode)
                return;

            // Giả sử trong code-behind bạn đã cập nhật CurrentFilter và các flag None từ UI
            // Sau đó gọi ApplyFilterAndSort
            ApplyFilterAndSort(true);
            // Cập nhật trạng thái HasActiveFilter
            OnPropertyChanged(nameof(HasActiveFilter));
        }

        public void NotifyHasActiveFilterChanged()
        {
            OnPropertyChanged(nameof(HasActiveFilter));
        }

        partial void OnIsReverseSortChanged(bool value)
        {
            // Không cho phép reverse sort trong PlayQueue mode
            if (IsPlayQueueMode)
                return;

            if (!IsPlayQueueMode)
            {
                ApplyFilterAndSort(false);
            }
        }

        // Khi IncludeMetaScoreNone hoặc IncludeAvgPlayNone thay đổi, cập nhật HasActiveFilter
        partial void OnIncludeMetaScoreNoneChanged(bool value)
        {
            OnPropertyChanged(nameof(HasActiveFilter));
        }

        partial void OnIncludeAvgPlayNoneChanged(bool value)
        {
            OnPropertyChanged(nameof(HasActiveFilter));
        }
    }
}