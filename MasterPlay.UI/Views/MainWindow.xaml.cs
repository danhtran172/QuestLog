using MasterPlay.Helpers;
using MasterPlay.Models;
using MasterPlay.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MasterPlay.UI
{
    public partial class MainWindow : Window
    {
        #region Constants

        private const int DefaultPageSize = 30;

        #endregion

        #region Properties

        public ObservableCollection<GameViewModel> Games { get; } = new ObservableCollection<GameViewModel>();

        #endregion

        #region Fields

        private string _searchText = string.Empty;
        private string _viewMode = "All";
        private string _searchBy = "All";

        private int _currentPage = 1;
        private int _pageSize = DefaultPageSize;
        private int _totalItems = 0;

        private List<GameViewModel> _filteredGamesCache = new List<GameViewModel>();

        #endregion

        #region Constructor

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            LoadGamesData();
        }

        #endregion

        #region Data Loading

        public void LoadGamesData()
        {
            DataService.SortGames();
            DataService.SyncReadyGames();
            ApplyFilterAndSort(resetToFirstPage: false);
        }

        #endregion

        #region Filtering & Sorting

        private void ApplyFilterAndSort(bool resetToFirstPage = true)
        {
            if (resetToFirstPage)
                _currentPage = 1;

            var allData = DataService.LoadGames()
                                     .Where(g => !g.IsDisabled)
                                     .Select(g => new GameViewModel(g))
                                     .AsQueryable();

            // 1. Filter by View Mode
            if (_viewMode == "Plan")
            {
                allData = allData.Where(vm => vm.Game.Progress != "Done");
            }
            else if (_viewMode == "Finish")
            {
                allData = allData.Where(vm => vm.Game.Progress == "Done");
            }

            // 2. Filter by Search Text
            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                string s = _searchText.ToLower();

                switch (_searchBy)
                {
                    case "Name":
                        allData = allData.Where(vm => vm.Game.Name.ToLower().Contains(s));
                        break;

                    case "Series":
                        allData = allData.Where(vm => (vm.Game.Series ?? string.Empty).ToLower().Contains(s));
                        break;

                    case "Publisher":
                        allData = allData.Where(vm => (vm.Game.Publisher ?? string.Empty).ToLower().Contains(s));
                        break;

                    case "Developer":
                        allData = allData.Where(vm => (vm.Game.Developer ?? string.Empty).ToLower().Contains(s));
                        break;

                    case "Release":
                        if (int.TryParse(s, out int year))
                        {
                            allData = allData.Where(vm => vm.Game.PublishDate.HasValue &&
                                                          vm.Game.PublishDate.Value.Year == year);
                        }
                        else
                        {
                            allData = allData.Where(_ => false);
                        }
                        break;

                    default:
                        allData = allData.Where(vm =>
                            vm.Game.Name.ToLower().Contains(s) ||
                            (vm.Game.Series ?? string.Empty).ToLower().Contains(s));
                        break;
                }
            }

            // 3. Sorting
            int sortIndex = SortComboBox?.SelectedIndex ?? 0;

            allData = sortIndex switch
            {
                0 => allData.OrderByDescending(vm => vm.Game.Ready)
                            .ThenByDescending(vm => vm.Game.PublishDate),

                1 => allData.OrderByDescending(vm => vm.Game.Ready)
                            .ThenByDescending(vm => vm.Game.PublishDate)
                            .ThenBy(vm => vm.Game.Name),

                2 => allData.OrderBy(vm => vm.Game.Name)
                            .ThenBy(vm => vm.Game.Ready)
                            .ThenBy(vm => vm.Game.TotalPlayTimeSeconds)
                            .ThenByDescending(vm => vm.Game.PublishDate),

                3 => allData.OrderByDescending(vm => vm.Game.TotalPlayTimeSeconds)
                            .ThenBy(vm => vm.Game.Ready)
                            .ThenBy(vm => vm.Game.Name)
                            .ThenByDescending(vm => vm.Game.PublishDate),

                4 => allData.OrderByDescending(vm => vm.Game.PublishDate)
                            .ThenBy(vm => vm.Game.Name)
                            .ThenBy(vm => vm.Game.Ready)
                            .ThenBy(vm => vm.Game.TotalPlayTimeSeconds),

                _ => allData.OrderByDescending(vm => vm.Game.Ready)
                            .ThenBy(vm => vm.Game.Name)
                            .ThenBy(vm => vm.Game.TotalPlayTimeSeconds)
                            .ThenBy(vm => vm.Game.PublishDate)
            };

            _filteredGamesCache = allData.ToList();
            _totalItems = _filteredGamesCache.Count;

            RefreshCurrentPageItems();
        }

        private void RefreshCurrentPageItems()
        {
            int totalPages = GetTotalPages();

            if (_currentPage < 1)
                _currentPage = 1;

            if (_currentPage > totalPages)
                _currentPage = totalPages;

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

            UpdatePaginationUI();
        }

        private int GetTotalPages()
        {
            return Math.Max(1, (int)Math.Ceiling(_totalItems / (double)_pageSize));
        }

        private void UpdatePaginationUI()
        {
            int totalPages = GetTotalPages();

            if (PrevPageButton != null)
                PrevPageButton.IsEnabled = _currentPage > 1;

            if (NextPageButton != null)
                NextPageButton.IsEnabled = _currentPage < totalPages;

            if (PageInfoTextBlock != null)
                PageInfoTextBlock.Text = $"Page {_currentPage} / {totalPages}";

            if (PaginationSummaryTextBlock != null)
            {
                if (_totalItems == 0)
                {
                    PaginationSummaryTextBlock.Text = "Showing 0 items";
                }
                else
                {
                    int start = ((_currentPage - 1) * _pageSize) + 1;
                    int end = Math.Min(_currentPage * _pageSize, _totalItems);
                    PaginationSummaryTextBlock.Text = $"Showing {start}-{end} of {_totalItems}";
                }
            }
        }

        #endregion

        #region UI Reset

        private void ResetUIState()
        {
            _searchText = string.Empty;
            _viewMode = "All";
            _searchBy = "All";
            _currentPage = 1;

            if (SearchBox != null)
                SearchBox.Text = string.Empty;

            if (SearchByComboBox != null)
                SearchByComboBox.SelectedIndex = 0;

            if (ViewModeComboBox != null)
                ViewModeComboBox.SelectedIndex = 0;

            if (SortComboBox != null)
                SortComboBox.SelectedIndex = 0;
        }

        #endregion

        #region Pagination Events

        private void PrevPageButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage <= 1) return;

            _currentPage--;
            RefreshCurrentPageItems();
        }

        private void NextPageButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage >= GetTotalPages()) return;

            _currentPage++;
            RefreshCurrentPageItems();
        }

        #endregion

        #region Event Handlers

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchText = (sender as TextBox)?.Text ?? string.Empty;
            ApplyFilterAndSort(resetToFirstPage: true);
        }

        private void ViewModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _viewMode = (ViewModeComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "All";
            ApplyFilterAndSort(resetToFirstPage: true);
        }

        private void SearchByComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _searchBy = (SearchByComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "All";

            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                ApplyFilterAndSort(resetToFirstPage: true);
            }
        }

        private void SortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
            {
                ApplyFilterAndSort(resetToFirstPage: true);
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                ResetUIState();
                LoadGamesData();
            }
            if (e.Key == Key.F11)
            {
                WindowState = WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;

                e.Handled = true;
            }
        }

        private void AddGameButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var addWindow = new AddGameWindow();
                addWindow.Owner = this;
                addWindow.ShowDialog();
                LoadGamesData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening Add window: {ex.Message}\n\n{ex.StackTrace}", "Crash Debug", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow();
            settingsWindow.Owner = this;
            settingsWindow.ShowDialog();
            LoadGamesData();
        }

        private void EditGame_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is GameViewModel gameVm)
            {
                try
                {
                    if (gameVm.Game == null) return;

                    var editWindow = new EditGameWindow(gameVm.Game);
                    editWindow.Owner = this;
                    editWindow.ShowDialog();
                    LoadGamesData();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error opening edit window: {ex.Message}", "Notification");
                }
            }
        }

        private void PlayGame_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is GameViewModel vm)
            {
                try
                {
                    string processPath = vm.GetProcessPath();

                    if (!string.IsNullOrEmpty(processPath))
                    {
                        FileHelper.RunProcess(processPath);
                        LoadGamesData();
                    }
                    else
                    {
                        MessageBox.Show("Game executable path not found.", "Notification", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to launch game: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion
    }
}