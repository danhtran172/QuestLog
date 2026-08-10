using System.Windows;
using System.Windows.Input;

namespace MasterPlay.UI.Services.Input;

/// <summary>
/// Identifies a focusable game card and provides the controller actions for it.
/// </summary>
public static class ControllerNavigationProperties
{
    public static readonly DependencyProperty SelectCommandProperty =
        DependencyProperty.RegisterAttached(
            "SelectCommand",
            typeof(ICommand),
            typeof(ControllerNavigationProperties));

    public static readonly DependencyProperty PlayCommandProperty =
        DependencyProperty.RegisterAttached(
            "PlayCommand",
            typeof(ICommand),
            typeof(ControllerNavigationProperties));

    public static void SetSelectCommand(DependencyObject element, ICommand? value) =>
        element.SetValue(SelectCommandProperty, value);

    public static ICommand? GetSelectCommand(DependencyObject element) =>
        (ICommand?)element.GetValue(SelectCommandProperty);

    public static void SetPlayCommand(DependencyObject element, ICommand? value) =>
        element.SetValue(PlayCommandProperty, value);

    public static ICommand? GetPlayCommand(DependencyObject element) =>
        (ICommand?)element.GetValue(PlayCommandProperty);
}
