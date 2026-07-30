using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace MasterPlay.UI.Shared.Controls;

public partial class FireValueControl : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(FireValueControl),
        new PropertyMetadata(string.Empty, OnPresentationChanged));

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value),
        typeof(object),
        typeof(FireValueControl),
        new PropertyMetadata(null, OnPresentationChanged));

    public FireValueControl()
    {
        InitializeComponent();
        UpdatePresentation();
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public object? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    private static void OnPresentationChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        ((FireValueControl)dependencyObject).UpdatePresentation();
    }

    private void UpdatePresentation()
    {
        var isPerfect = TryGetNumericValue(Value, out var value) && Math.Abs(value - 999) < double.Epsilon;
        ValueText.Text = isPerfect ? "10" : Text;
        FireImage.Visibility = isPerfect ? Visibility.Visible : Visibility.Collapsed;
    }

    private static bool TryGetNumericValue(object? input, out double value)
    {
        if (input is IConvertible convertible)
        {
            try
            {
                value = convertible.ToDouble(CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
            }
        }

        value = 0;
        return false;
    }
}
