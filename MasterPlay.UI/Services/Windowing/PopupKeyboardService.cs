using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MasterPlay.UI.Services.Windowing;

public static class PopupKeyboardService
{
    public static bool CloseOpenComboBox(DependencyObject root)
    {
        if (root is ComboBox { IsDropDownOpen: true } comboBox)
        {
            comboBox.IsDropDownOpen = false;
            return true;
        }

        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < childCount; index++)
        {
            if (CloseOpenComboBox(VisualTreeHelper.GetChild(root, index)))
                return true;
        }

        return false;
    }
}
