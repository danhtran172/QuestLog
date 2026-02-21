using MasterPlay.Helpers;
using MasterPlay.Models;
using MasterPlay.UI.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MasterPlay.UI
{
    public partial class MainWindow : Window
    {
        // ==================== PROPERTIES ====================
        public ObservableCollection<GameViewModel> Games { get; } = new ObservableCollection<GameViewModel>();

        private string _searchText = string.Empty;
        private string _viewMode = "All";
        private string _searchBy = "All";

        // ==================== CONSTRUCTOR ====================
        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            LoadGamesData();
        }

        // ==================== DATA LOADING ====================
        public void LoadGamesData()
        {
            DataService.SortGames();
            DataService.SyncReadyGames();
            ApplyFilterAndSort();
        }

        // ==================== FILTERING & SORTING ====================
        private void ApplyFilterAndSort()
        {
            // Always load from original data source to avoid losing data during filtering
            var allData = DataService.LoadGames()
                                     .Where(g => !g.IsDisabled)
                                     .Select(g => new GameViewModel(g))
                                     .AsQueryable();

            // 1. Filter by View Mode
            if (_viewMode == "Plan") allData = allData.Where(vm => vm.Game.Progress != "Done");
            else if (_viewMode == "Finish") allData = allData.Where(vm => vm.Game.Progress == "Done");

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
                        allData = allData.Where(vm => (vm.Game.Series ?? "").ToLower().Contains(s));
                        break;
                    case "Publisher":
                        allData = allData.Where(vm => (vm.Game.Publisher ?? "").ToLower().Contains(s));
                        break;
                    case "Developer":
                        allData = allData.Where(vm => (vm.Game.Developer ?? "").ToLower().Contains(s));
                        break;
                    case "Release":
                        if (int.TryParse(s, out int y))
                            allData = allData.Where(vm => vm.Game.PublishDate.HasValue && vm.Game.PublishDate.Value.Year == y);
                        break;
                    default:
                        allData = allData.Where(vm => vm.Game.Name.ToLower().Contains(s) ||
                                                      (vm.Game.Series ?? "").ToLower().Contains(s));
                        break;
                }
            }

            // 3. Sorting
            int sortIndex = SortComboBox?.SelectedIndex ?? 0;
            allData = sortIndex switch
            {
                0 => allData.OrderByDescending(vm => vm.Game.Ready).ThenByDescending(vm => vm.Game.PublishDate),
                1 => allData.OrderByDescending(vm => vm.Game.Ready).ThenByDescending(vm => vm.Game.PublishDate).ThenBy(vm => vm.Game.Name),
                2 => allData.OrderBy(vm => vm.Game.Name).ThenBy(vm => vm.Game.Ready).ThenBy(vm => vm.Game.TotalPlayTimeSeconds).ThenByDescending(vm => vm.Game.PublishDate),
                3 => allData.OrderByDescending(vm => vm.Game.TotalPlayTimeSeconds).ThenBy(vm => vm.Game.Ready).ThenBy(vm => vm.Game.Name).ThenByDescending(vm => vm.Game.PublishDate),
                4 => allData.OrderByDescending(vm => vm.Game.PublishDate).ThenBy(vm => vm.Game.Name).ThenBy(vm => vm.Game.Ready).ThenBy(vm => vm.Game.TotalPlayTimeSeconds),
                _ => allData.OrderByDescending(vm => vm.Game.Ready).ThenBy(vm => vm.Game.Name).ThenBy(vm => vm.Game.TotalPlayTimeSeconds).ThenBy(vm => vm.Game.PublishDate)
            };

            // 4. Update UI immediately
            Games.Clear();
            foreach (var item in allData.ToList())
            {
                Games.Add(item);
            }
        }
        // ==================== UI RESET ====================
        private void ResetUIState()
        {
            _searchText = string.Empty;
            _viewMode = "All";
            _searchBy = "All";

            if (SearchBox != null)
                SearchBox.Text = string.Empty;

            if (SearchByComboBox != null)
                SearchByComboBox.SelectedIndex = 0;

            if (ViewModeComboBox != null)
                ViewModeComboBox.SelectedIndex = 0;

            if (SortComboBox != null)
                SortComboBox.SelectedIndex = 0;
        }

        // ==================== EVENT HANDLERS ====================
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchText = (sender as TextBox)?.Text ?? "";
            ApplyFilterAndSort();
        }

        private void ViewModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _viewMode = (ViewModeComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "All";
            ApplyFilterAndSort();
        }

        private void SearchByComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _searchBy = (SearchByComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "All";
            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                ApplyFilterAndSort();
            }
         
        }

        private void SortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) ApplyFilterAndSort();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                ResetUIState();
                LoadGamesData();
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
    }
}