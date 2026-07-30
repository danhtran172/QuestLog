using CommunityToolkit.Mvvm.Messaging;
using MasterPlay.Infrastructure.Events;
using MasterPlay.UI.Features.Queue;
using MasterPlay.UI.Services.Runtime;
using MasterPlay.UI.Services.Windowing;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MasterPlay.UI.Features.Library
{
    public partial class MainWindow : Window
    {
        #region Fields

        private readonly MainWindowViewModel _viewModel;
        private readonly GameProcessMonitor _gameProcessMonitor;
        private bool _isInitializing;

        #endregion

        #region Constructor

        public MainWindow()
        {
            _viewModel = new MainWindowViewModel();
            _gameProcessMonitor = new GameProcessMonitor();
            _isInitializing = true;

            InitializeComponent();

            DataContext = _viewModel;
            FilterBackdrop.MouseLeftButtonDown += FilterBackdrop_MouseLeftButtonDown;
            WeakReferenceMessenger.Default.Register<RefreshUIMessage>(this, (r, m) =>
            {
                Dispatcher.Invoke(_viewModel.LoadGamesData);
            });
            _gameProcessMonitor.RunningProcessesChanged += OnRunningProcessesChanged;
            Loaded += OnLoaded;
            Closed += OnClosed;

            _isInitializing = false;
            _viewModel.LoadGamesData();
        }

        #endregion

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _gameProcessMonitor.Start();
        }

        private void OnRunningProcessesChanged(IReadOnlySet<string> runningProcessNames)
        {
            if (Dispatcher.HasShutdownStarted)
                return;

            Dispatcher.BeginInvoke(() => _viewModel.UpdateRunningStates(runningProcessNames));
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            _gameProcessMonitor.RunningProcessesChanged -= OnRunningProcessesChanged;
            _gameProcessMonitor.Dispose();
            WeakReferenceMessenger.Default.UnregisterAll(this);
        }

        private void SearchByComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || _viewModel == null)
                return;

            string searchBy = (SearchByComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "All";
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
                _viewModel.AddGameCommand.Execute(null);
                e.Handled = true;
            }
            if (e.Key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None)
            {
                if (FilterPopup.IsOpen)
                {
                    FilterPanel_CloseRequested(null, EventArgs.Empty);
                    e.Handled = true;
                }
                else if (PopupKeyboardService.CloseOpenComboBox(this))
                {
                    e.Handled = true;
                }
                return;
            }
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.D1:
                        _viewModel.ShowAllCommand.Execute(null);
                        e.Handled = true;
                        break;
                    case Key.D2:
                        _viewModel.ShowPlanningCommand.Execute(null);
                        e.Handled = true;
                        break;
                    case Key.D3:
                        _viewModel.ShowFinishedCommand.Execute(null);
                        e.Handled = true;
                        break;
                    case Key.D4:
                        _viewModel.ShowPlayQueueCommand.Execute(null);
                        e.Handled = true;
                        break;
                }
            }
        }

        private void FilterPanel_CloseRequested(object? sender, EventArgs e)
        {
            FilterPopup.IsOpen = false;
            FilterToggleButton.IsChecked = false;
        }

        private void FilterBackdrop_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            FilterPanel_CloseRequested(null, EventArgs.Empty);
            e.Handled = true;
        }
    }
}
