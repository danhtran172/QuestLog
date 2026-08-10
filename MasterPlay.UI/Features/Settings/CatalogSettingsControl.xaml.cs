using System.Windows.Controls;
using System.Windows.Input;

namespace MasterPlay.UI.Features.Settings;

public partial class CatalogSettingsControl : UserControl
{
    public CatalogSettingsControl() => InitializeComponent();

    private void Items_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        ItemsScrollViewer.ScrollToVerticalOffset(ItemsScrollViewer.VerticalOffset - e.Delta / 3d);
        e.Handled = true;
    }
}
