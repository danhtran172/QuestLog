using System.Windows;
using System.Windows.Controls;
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
            // Tìm ScrollViewer bên trong ListView
            var scrollViewer = FindVisualChild<ScrollViewer>(QueueListView);
            if (scrollViewer != null)
            {
                scrollViewer.ScrollToTop();
            }
            else if (QueueListView.Items.Count > 0)
            {
                // Fallback nếu không tìm thấy ScrollViewer
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
    }
}