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
        public ObservableCollection<GameViewModel> Games { get; } = new ObservableCollection<GameViewModel>();
        private string _searchText = string.Empty;
        private string _viewMode = "All";

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            LoadGamesData();
            SearchByComboBox.SelectionChanged += SearchByComboBox_SelectionChanged;
            ViewModeComboBox.SelectionChanged += ViewModeComboBox_SelectionChanged;
        }

        public void LoadGamesData()
        {
            Games.Clear();
            DataService.SortGames();
            DataService.SyncReadyGames();

            var activeGames = DataService.LoadGames()
                                 .Where(g => !g.IsDisabled)
                                 .OrderBy(g => g.Ready)
                                 .ToList();

            foreach (var game in activeGames)
            {
                var vm = new GameViewModel(game);
                Games.Add(vm);
            }

            ApplyFilterAndSort();

            if (activeGames.Count == 0)
            {
                MessageBox.Show("There are no active games.",
                                "Notification", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                LoadGamesData();
                e.Handled = true;
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                _searchText = textBox.Text?.Trim().ToLowerInvariant() ?? string.Empty;
                ApplyFilterAndSort();
            }
        }
        private void ViewModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                _viewMode = selectedItem.Content.ToString();
                ApplyFilterAndSort();
            }
        }

        private void SortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilterAndSort();
        }

        private string _searchBy = "All"; // Default là All

        private void SearchByComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                _searchBy = selectedItem.Content.ToString();
                ApplyFilterAndSort();
            }
        }

        private void ApplyFilterAndSort()
        {
            var query = Games.AsQueryable();

            // 1. Lọc theo View Mode
            switch (_viewMode)
            {
                case "All":
                    // Không lọc gì, hiển thị tất cả
                    break;

                case "Plan":
                    // Ẩn game có Progress = "Done" (giả sử Progress là string "Done")
                    query = query.Where(vm => vm.Game.Progress != "Done");
                    break;

                case "Finish":
                    // Chỉ lấy game có Progress = "Done"
                    query = query.Where(vm => vm.Game.Progress == "Done");
                    break;
            }

            // 2. Lọc theo Search Text (giữ nguyên logic cũ của bạn)
            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                string searchLower = _searchText.ToLowerInvariant();

                switch (_searchBy)
                {
                    case "All":
                    case "Name":
                        query = query.Where(vm => vm.Game.Name.ToLowerInvariant().Contains(searchLower));
                        break;

                    case "Series":
                        query = query.Where(vm => vm.Game.Series.ToLowerInvariant().Contains(searchLower) == true);
                        break;

                    case "Publisher":
                        query = query.Where(vm => vm.Game.Publisher.ToLowerInvariant().Contains(searchLower) == true);
                        break;

                    case "Developer":
                        query = query.Where(vm => vm.Game.Developer.ToLowerInvariant().Contains(searchLower) == true);
                        break;

                    case "Publish Date (Year)":
                        if (int.TryParse(_searchText, out int year))
                        {
                            query = query.Where(vm => vm.Game.PublishDate.GetValueOrDefault().Year == year);
                        }
                        else
                        {
                            query = query.Where(vm => false);
                        }
                        break;
                }
            }

            // 3. Sort theo View Mode + Sort Combo (ưu tiên View Mode sort trước)
            switch (_viewMode)
            {
                case "Plan":
                    // Plan: sort Progress → Excited → Avg Runtime → Name
                    query = query.OrderBy(vm => vm.Game.Progress)           // Progress đầu tiên
                                 .ThenByDescending(vm => vm.Game.Excited)   // Excited giảm dần
                                 .ThenBy(vm => vm.Game.AvgPlayTime)             // Runtime tăng dần (Avg Runtime giả sử là RunTime)
                                 .ThenBy(vm => vm.Game.Name);
                    break;

                case "Finish":
                    // Finish: sort Score → FinishIn → PublishDate
                    query = query.OrderByDescending(vm => vm.Game.Score)    // Score giảm dần
                                 .ThenByDescending(vm => vm.Game.FinishedIn) // FinishIn giảm dần (mới xong trước)
                                 .ThenByDescending(vm => vm.Game.PublishDate); // PublishDate giảm dần
                    break;

                default: // All hoặc các mode khác: dùng SortComboBox
                    int sortIndex = SortComboBox?.SelectedIndex ?? 0;
                    switch (sortIndex)
                    {
                        case 0:
                            query = query.OrderByDescending(vm => vm.Game.Ready)
                                         .ThenBy(vm => vm.Game.TotalPlayTimeSeconds)
                                         .ThenBy(vm => vm.Game.Name);
                            break;
                        case 1:
                            query = query.OrderByDescending(vm => vm.Game.Ready);
                            break;
                        case 2:
                            query = query.OrderBy(vm => vm.Game.Name);
                            break;
                        case 3:
                            query = query.OrderByDescending(vm => vm.Game.TotalPlayTimeSeconds);
                            break;
                    }
                    break;
            }

            // 4. Áp dụng kết quả (giữ nguyên cách cũ của bạn)
            var result = query.ToList();
            Games.Clear();
            foreach (var vm in result)
            {
                Games.Add(vm);
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
                    MessageBox.Show($"Lỗi khi mở chỉnh sửa: {ex.Message}", "Notification");
                }
            }
        }

        private void PlayGame_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is GameViewModel vm)
            {
                try
                {
                    string processPath = vm.GetProcessPath();  // Gọi từ ViewModel
                    if (!string.IsNullOrEmpty(processPath))
                    {
                        FileHelper.RunProcess(processPath);
                        LoadGamesData();
                    }
                    else
                    {
                        MessageBox.Show("Không tìm thấy đường dẫn thực thi của game.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Không thể chạy game: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        public RelayCommand(Action execute) => _execute = execute;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute?.Invoke();
        public event EventHandler? CanExecuteChanged;
    }
}