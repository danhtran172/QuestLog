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
        public ObservableCollection<GameViewModel> FilteredGames { get; } = new ObservableCollection<GameViewModel>();

        private string _searchText = string.Empty;

        public ICommand RefreshCommand { get; }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            RefreshCommand = new RelayCommand(LoadGamesData);
            LoadGamesData();
        }

        private void LoadGamesData()
        {
            Games.Clear();
            FilteredGames.Clear();

            Data.SortGames();
            Data.SyncReadyGames();

            var activeGames = Data.LoadGames()
                                 .Where(g => !g.IsDisabled)
                                 .OrderBy(g => g.Ready)
                                 .ToList();

            foreach (var game in activeGames)
            {
                var vm = new GameViewModel(game);
                Games.Add(vm);
                FilteredGames.Add(vm);
                
            }

            ApplyFilterAndSort();

            if (activeGames.Count == 0)
            {
                MessageBox.Show("There are no active games.",
                                "Notification", MessageBoxButton.OK, MessageBoxImage.Information);
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

        private void SortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilterAndSort();
        }

        private void ApplyFilterAndSort()
        {
            var query = Games.AsQueryable();

            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                query = query.Where(vm =>
                    vm.Game.Name.ToLowerInvariant().Contains(_searchText) == true);
            }

            int sortIndex = SortComboBox?.SelectedIndex ?? 0;
            switch (sortIndex)
            {


                case 0: // Playable First (Ready = true trước)
                    query = query.OrderByDescending(vm => vm.Game.Ready)
                                 
                                 .ThenBy(vm => vm.Game.TotalPlayTimeSeconds)
                                 .ThenBy(vm => vm.Game.Name);
                    break;
                case 1: // Playable First (Ready = true trước)
                    query = query.OrderByDescending(vm => vm.Game.Ready);
                    break;
                case 2: // Name (A-Z)
                    query = query.OrderBy(vm => vm.Game.Name);
                    break;
                case 3: // Play Time (descending)
                    query = query.OrderByDescending(vm => vm.Game.TotalPlayTimeSeconds);
                    break;
            }

            var result = query.ToList();

            FilteredGames.Clear();
            foreach (var vm in result)
            {
                FilteredGames.Add(vm);
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

                    if (editWindow.ShowDialog() == true)
                    {
                        LoadGamesData();
                    }
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
                    string processPath = vm.getProcessPath();
                    if (!string.IsNullOrEmpty(processPath))
                    {
                        FileHelper.RunProcess(processPath);
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

    // RelayCommand đơn giản
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        public RelayCommand(Action execute) => _execute = execute;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute?.Invoke();
        public event EventHandler? CanExecuteChanged;
    }
}