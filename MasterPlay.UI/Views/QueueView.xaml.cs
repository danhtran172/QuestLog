using MasterPlay.UI.ViewModels;
using MasterPlay.UI.Views;
using MasterPlay.Core.Localization;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MasterPlay.UI.Views
{
    public partial class QueueView : UserControl
    {
        public QueueView()
        {
            InitializeComponent();
        }


        public void ScrollToTop()
        {
            var scrollViewer = FindVisualChild<ScrollViewer>(QueueListView);
            if (scrollViewer != null)
            {
                scrollViewer.ScrollToTop();
            }
            else if (QueueListView.Items.Count > 0)
            {
                QueueListView.ScrollIntoView(QueueListView.Items[0]);
            }
        }

        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child != null && child is T t)
                    return t;

                var descendant = FindVisualChild<T>(child);
                if (descendant != null)
                    return descendant;
            }
            return null;
        }


        private void QueueListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var listView = sender as ListView;
            if (listView?.SelectedItem is GameViewModel gameVm && gameVm.Game != null)
            {
                OpenEditWindow(gameVm);
            }
        }


        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is GameViewModel gameVm && gameVm.Game != null)
            {
                OpenEditWindow(gameVm);
            }
        }


        private void OpenEditWindow(GameViewModel gameVm)
        {
            try
            {
                var mainWindow = Window.GetWindow(this) as MainWindow;
                var editWindow = new GameEditorWindow(gameVm.Game)
                {
                    Owner = mainWindow
                };

                editWindow.ShowDialog();


                if (DataContext is MainWindowViewModel viewModel)
                {
                    viewModel.LoadGamesData();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(LocalizationService.Get("ErrorOpeningEditor", ex.Message), LocalizationService.Get("Notification"));
            }
        }
    }
}
