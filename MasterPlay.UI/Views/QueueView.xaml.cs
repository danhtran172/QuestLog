using MasterPlay.UI.ViewModels;
using MasterPlay.UI.Views;
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

        // ==================== SCROLL TO TOP ====================
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

        // ==================== DOUBLE-CLICK HANDLER ====================
        private void QueueListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var listView = sender as ListView;
            if (listView?.SelectedItem is GameViewModel gameVm && gameVm.Game != null)
            {
                OpenEditWindow(gameVm);
            }
        }

        // ==================== EDIT BUTTON CLICK (nếu có button trong tương lai) ====================
        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is GameViewModel gameVm && gameVm.Game != null)
            {
                OpenEditWindow(gameVm);
            }
        }

        // ==================== CHUNG: MỞ EDIT WINDOW ====================
        private void OpenEditWindow(GameViewModel gameVm)
        {
            try
            {
                var mainWindow = Window.GetWindow(this) as MainWindow;
                var editWindow = new EditGameWindow(gameVm.Game)
                {
                    Owner = mainWindow
                };

                editWindow.ShowDialog();

                // Refresh data after editing
                if (DataContext is MainWindowViewModel viewModel)
                {
                    viewModel.LoadGamesData();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening edit window: {ex.Message}", "Notification");
            }
        }
    }
}