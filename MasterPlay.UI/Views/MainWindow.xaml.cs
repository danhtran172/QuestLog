using MasterPlay.Helpers;
using MasterPlay.UI.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MasterPlay.UI
{
    public partial class MainWindow : Window
    {
        #region Fields

        private readonly MainWindowViewModel _viewModel;
        private bool _isInitializing;

        #endregion

        #region Constructor

        public MainWindow()
        {
            _viewModel = new MainWindowViewModel();
            _isInitializing = true;

            InitializeComponent();

            DataContext = _viewModel;

            _isInitializing = false;
            _viewModel.LoadGamesData();
        }

        #endregion

        #region UI Event Handlers

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing || _viewModel == null)
                return;

            _viewModel.SetSearchText((sender as TextBox)?.Text ?? string.Empty, applyImmediately: true);
        }

        private void ViewModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || _viewModel == null)
                return;

            string viewMode = (ViewModeComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All";
            _viewModel.SetViewMode(viewMode);
        }

        private void SearchByComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || _viewModel == null)
                return;

            string searchBy = (SearchByComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All";
            _viewModel.SetSearchBy(searchBy);
        }

        private void SortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || _viewModel == null || !IsLoaded)
                return;

            _viewModel.SetSortIndex(SortComboBox?.SelectedIndex ?? 0);
        }

        private void PrevPageButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel?.GoToPreviousPage();
        }

        private void NextPageButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel?.GoToNextPage();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (_viewModel == null)
                return;

            if (e.Key == Key.F5)
            {
                _viewModel.ResetUIState();

                if (SearchBox != null) SearchBox.Text = string.Empty;
                if (SearchByComboBox != null) SearchByComboBox.SelectedIndex = 0;
                if (ViewModeComboBox != null) ViewModeComboBox.SelectedIndex = 0;
                if (SortComboBox != null) SortComboBox.SelectedIndex = 0;

                _viewModel.LoadGamesData();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.F11)
            {
                WindowState = WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;

                e.Handled = true;
            }
        }

        #endregion

        #region Window Actions

        private void AddGameButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var addWindow = new AddGameWindow
                {
                    Owner = this
                };

                addWindow.ShowDialog();
                _viewModel.LoadGamesData();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error opening Add window: {ex.Message}\n\n{ex.StackTrace}",
                    "Crash Debug",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow
            {
                Owner = this
            };

            settingsWindow.ShowDialog();
            _viewModel.LoadGamesData();
        }

        private void EditGame_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.DataContext is not GameViewModel gameVm)
                return;

            try
            {
                if (gameVm.Game == null)
                    return;

                var editWindow = new EditGameWindow(gameVm.Game)
                {
                    Owner = this
                };

                editWindow.ShowDialog();
                _viewModel.LoadGamesData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening edit window: {ex.Message}", "Notification");
            }
        }

        private void PlayGame_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.DataContext is not GameViewModel vm)
                return;

            try
            {
                string processPath = vm.GetProcessPath();

                if (!string.IsNullOrEmpty(processPath))
                {
                    FileHelper.RunProcess(processPath);
                    _viewModel.LoadGamesData();
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

        #endregion
    }
}