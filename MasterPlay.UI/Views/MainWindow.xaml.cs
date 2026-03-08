using CommunityToolkit.Mvvm.Messaging;
using MasterPlay.Helpers;
using MasterPlay.Models.Repositories;
using MasterPlay.UI.ViewModels;
using MasterPlay.UI.Views;
using System;
using System.Diagnostics;
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
            WeakReferenceMessenger.Default.Register<RefreshUIMessage>(this, (r, m) =>
            {
                Dispatcher.Invoke(() =>
                {
                    _viewModel.LoadGamesData();
                });
            });

            _isInitializing = false;
            _viewModel.LoadGamesData();
        }

        #endregion

        private void OnRequestRefresh()
        {
            Debug.WriteLine("OnRequestRefresh called");
            Dispatcher.Invoke(() =>
            {
                Debug.WriteLine("LoadGamesData on UI thread");
                _viewModel.LoadGamesData();
            });
        }

        #region UI Event Handlers

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing || _viewModel == null)
                return;

            _viewModel.SetSearchText((sender as TextBox)?.Text ?? string.Empty, applyImmediately: true);
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
            if (_viewModel == null) return;
            _viewModel.GoToPreviousPage();
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_viewModel.IsPlayQueueMode)
                {
                    var queueView = ListBorder?.Child as QueueView;
                    queueView?.ScrollToTop();
                }
                else
                {
                    CardScrollViewer?.ScrollToTop();
                }
            }));
        }

        private void NextPageButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;
            _viewModel.GoToNextPage();
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_viewModel.IsPlayQueueMode)
                {
                    var queueView = ListBorder?.Child as QueueView;
                    queueView?.ScrollToTop();
                }
                else
                {
                    CardScrollViewer?.ScrollToTop();
                }
            }));
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (_viewModel == null)
                return;

            if (e.Key == Key.F5)
            {
                _viewModel.LoadGamesData();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.F6)
            {
                _viewModel.ResetUIState();

                if (SearchBox != null) SearchBox.Text = string.Empty;
                if (SearchByComboBox != null) SearchByComboBox.SelectedIndex = 0;
                _viewModel.SetViewMode("All");
                if (SortComboBox != null) SortComboBox.SelectedIndex = 0;
                _viewModel.IsReverseSort = false;

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
            if (e.Key == Key.Oem3 && Keyboard.Modifiers == ModifierKeys.None)
            {
                AddGameButton_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
            if (e.Key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None)
            {
                SettingsButton_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.D1:
                        AllButton_Click(this, new RoutedEventArgs());
                        e.Handled = true;
                        break;
                    case Key.D2:
                        PlanButton_Click(this, new RoutedEventArgs());
                        e.Handled = true;
                        break;
                    case Key.D3:
                        FinishButton_Click(this, new RoutedEventArgs());
                        e.Handled = true;
                        break;
                    case Key.D4:
                        PlayQueueButton_Click(this, new RoutedEventArgs());
                        e.Handled = true;
                        break;
                }
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
                string processPath = vm.GetLaunchProcessPath();

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

        private void AllButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm)
                vm.SetViewMode("All");
        }

        private void PlanButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm)
                vm.SetViewMode("Plan");
        }

        private void FinishButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm)
                vm.SetViewMode("Finish");
        }
        private void PlayQueueButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm)
                vm.SetViewMode("PlayQueue");
        }

        private void ClearFilterButton_Click(object sender, RoutedEventArgs e)
        {
            ReverseCheckBox.IsChecked = false;

            FinishOperator.SelectedIndex = 0;
            FinishValue.Text = string.Empty;

            ExcitedOperator.SelectedIndex = 0;
            ExcitedValue.Text = string.Empty;

            ScoreOperator.SelectedIndex = 0;
            ScoreValue.Text = string.Empty;

            PlayTimeOperator.SelectedIndex = 0;
            PlayTimeValue.Text = string.Empty;

            AvgVeryLong.IsChecked = false;
            AvgLong.IsChecked = false;
            AvgMedium.IsChecked = false;
            AvgShort.IsChecked = false;
            AvgVeryShort.IsChecked = false;
            AvgNone.IsChecked = false;

            ProgressNextPlay.IsChecked = false;
            ProgressDone.IsChecked = false;
            ProgressPlaying.IsChecked = false;
            ProgressIntended.IsChecked = false;
            ProgressDrop.IsChecked = false;
            ProgressPending.IsChecked = false;

            OwnReady.IsChecked = false;
            OwnPirated.IsChecked = false;
            OwnGamePass.IsChecked = false;
            OwnNotOwned.IsChecked = false;
            OwnPSRequired.IsChecked = false;

            MetaVeryHigh.IsChecked = false;
            MetaHigh.IsChecked = false;
            MetaMedium.IsChecked = false;
            MetaLow.IsChecked = false;
            MetaVeryLow.IsChecked = false;
            MetaNone.IsChecked = false;

            _viewModel?.ClearFilter();
            _viewModel.NotifyHasActiveFilterChanged();
        }

        private void ApplyFilterButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;

            var filter = _viewModel.CurrentFilter;

            filter.IsReverse = ReverseCheckBox.IsChecked == true;

            filter.OperatorFinish = (FinishOperator.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "=";
            filter.ValueFinish = double.TryParse(FinishValue.Text, out double f) ? f : (double?)null;

            filter.OperatorExcited = (ExcitedOperator.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "=";
            filter.ValueExcited = double.TryParse(ExcitedValue.Text, out double ex) ? ex : (double?)null;

            filter.OperatorScore = (ScoreOperator.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "=";
            filter.ValueScore = double.TryParse(ScoreValue.Text, out double s) ? s : (double?)null;

            filter.OperatorPlayTime = (PlayTimeOperator.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "=";
            if (double.TryParse(PlayTimeValue.Text, out double hours))
            {
                filter.ValuePlayTime = (int)(hours * 3600);
            }
            else
            {
                filter.ValuePlayTime = null;
            }

            filter.SelectedAvgPlays.Clear();
            if (AvgVeryLong.IsChecked == true) filter.SelectedAvgPlays.Add(AvgPlayLevel.VeryLong);
            if (AvgLong.IsChecked == true) filter.SelectedAvgPlays.Add(AvgPlayLevel.Long);
            if (AvgMedium.IsChecked == true) filter.SelectedAvgPlays.Add(AvgPlayLevel.Medium);
            if (AvgShort.IsChecked == true) filter.SelectedAvgPlays.Add(AvgPlayLevel.Short);
            if (AvgVeryShort.IsChecked == true) filter.SelectedAvgPlays.Add(AvgPlayLevel.VeryShort);

            _viewModel.IncludeAvgPlayNone = AvgNone.IsChecked == true;

            filter.SelectedProgressIds.Clear();
            if (ProgressNextPlay.IsChecked == true) filter.SelectedProgressIds.Add(3);
            if (ProgressDone.IsChecked == true) filter.SelectedProgressIds.Add(1);
            if (ProgressPlaying.IsChecked == true) filter.SelectedProgressIds.Add(2);
            if (ProgressIntended.IsChecked == true) filter.SelectedProgressIds.Add(4);
            if (ProgressDrop.IsChecked == true) filter.SelectedProgressIds.Add(5);
            if (ProgressPending.IsChecked == true) filter.SelectedProgressIds.Add(6);

            filter.SelectedOwnershipIds.Clear();
            if (OwnReady.IsChecked == true) filter.SelectedOwnershipIds.Add(1);
            if (OwnPirated.IsChecked == true) filter.SelectedOwnershipIds.Add(2);
            if (OwnGamePass.IsChecked == true) filter.SelectedOwnershipIds.Add(3);
            if (OwnNotOwned.IsChecked == true) filter.SelectedOwnershipIds.Add(4);
            if (OwnPSRequired.IsChecked == true) filter.SelectedOwnershipIds.Add(5);

            filter.SelectedMetaScores.Clear();
            if (MetaVeryHigh.IsChecked == true) filter.SelectedMetaScores.Add(MetaScoreLevel.VeryHigh);
            if (MetaHigh.IsChecked == true) filter.SelectedMetaScores.Add(MetaScoreLevel.High);
            if (MetaMedium.IsChecked == true) filter.SelectedMetaScores.Add(MetaScoreLevel.Medium);
            if (MetaLow.IsChecked == true) filter.SelectedMetaScores.Add(MetaScoreLevel.Low);
            if (MetaVeryLow.IsChecked == true) filter.SelectedMetaScores.Add(MetaScoreLevel.VeryLow);

            _viewModel.IncludeMetaScoreNone = MetaNone.IsChecked == true;

            _viewModel.ApplyFilterAndSort(true);
            _viewModel.NotifyHasActiveFilterChanged();
            FilterPopup.IsOpen = false;
        }

        private void CancelFilterButton_Click(object sender, RoutedEventArgs e)
        {
            FilterPopup.IsOpen = false;
        }
    }
}