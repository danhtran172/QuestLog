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
                _viewModel.AddGameCommand.Execute(null);
                e.Handled = true;
            }
            if (e.Key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None)
            {
                _viewModel.OpenSettingsCommand.Execute(null);
                e.Handled = true;
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
    }
}
