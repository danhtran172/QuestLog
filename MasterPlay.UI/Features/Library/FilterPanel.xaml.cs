using MasterPlay.UI.Shared.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MasterPlay.UI.Features.Library;

public partial class FilterPanel : UserControl
{
    public event EventHandler? CloseRequested;

    public FilterPanel()
    {
        InitializeComponent();
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        ReverseCheckBox.IsChecked = false;
        FinishOperator.SelectedIndex = 0;
        FinishValue.Clear();
        ExcitedOperator.SelectedIndex = 0;
        ExcitedValue.Clear();
        ScoreOperator.SelectedIndex = 0;
        ScoreValue.Clear();
        PlayTimeOperator.SelectedIndex = 0;
        PlayTimeValue.Clear();

        foreach (var checkBox in GetFilterCheckBoxes())
            checkBox.IsChecked = false;

        viewModel.ClearFilter();
        viewModel.ApplyFilterAndSort(true);
        viewModel.NotifyHasActiveFilterChanged();
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        var filter = viewModel.CurrentFilter;
        filter.IsReverse = ReverseCheckBox.IsChecked == true;
        filter.OperatorFinish = GetOperator(FinishOperator);
        filter.ValueFinish = ParseDouble(FinishValue);
        filter.OperatorExcited = GetOperator(ExcitedOperator);
        filter.ValueExcited = ParseDouble(ExcitedValue);
        filter.OperatorScore = GetOperator(ScoreOperator);
        filter.ValueScore = ParseDouble(ScoreValue);
        filter.OperatorPlayTime = GetOperator(PlayTimeOperator);
        filter.ValuePlayTime = ParseDouble(PlayTimeValue) is { } hours
            ? (int)(hours * 3600)
            : null;

        ReplaceSelection(
            filter.SelectedAvgPlays,
            (AvgVeryLong, AvgPlayLevel.VeryLong),
            (AvgLong, AvgPlayLevel.Long),
            (AvgMedium, AvgPlayLevel.Medium),
            (AvgShort, AvgPlayLevel.Short),
            (AvgVeryShort, AvgPlayLevel.VeryShort));
        viewModel.IncludeAvgPlayNone = AvgNone.IsChecked == true;

        ReplaceSelection(
            filter.SelectedProgressIds,
            (ProgressNextPlay, 3),
            (ProgressDone, 1),
            (ProgressPlaying, 2),
            (ProgressIntended, 4),
            (ProgressDrop, 5),
            (ProgressPending, 6));

        ReplaceSelection(
            filter.SelectedOwnershipIds,
            (OwnReady, 1),
            (OwnPirated, 2),
            (OwnGamePass, 3),
            (OwnNotOwned, 4),
            (OwnPSRequired, 5),
            (OwnSteamOff, 6));

        ReplaceSelection(
            filter.SelectedMetaScores,
            (MetaVeryHigh, MetaScoreLevel.VeryHigh),
            (MetaHigh, MetaScoreLevel.High),
            (MetaMedium, MetaScoreLevel.Medium),
            (MetaLow, MetaScoreLevel.Low),
            (MetaVeryLow, MetaScoreLevel.VeryLow));
        viewModel.IncludeMetaScoreNone = MetaNone.IsChecked == true;

        viewModel.ApplyFilterAndSort(true);
        viewModel.NotifyHasActiveFilterChanged();
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void FilterPanel_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || Keyboard.Modifiers != ModifierKeys.None)
            return;

        CloseRequested?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private IEnumerable<CheckBox> GetFilterCheckBoxes()
    {
        return
        [
            AvgVeryLong, AvgLong, AvgMedium, AvgShort, AvgVeryShort, AvgNone,
            ProgressNextPlay, ProgressDone, ProgressPlaying, ProgressIntended, ProgressDrop, ProgressPending,
            OwnReady, OwnPirated, OwnGamePass, OwnNotOwned, OwnPSRequired, OwnSteamOff,
            MetaVeryHigh, MetaHigh, MetaMedium, MetaLow, MetaVeryLow, MetaNone
        ];
    }

    private static string GetOperator(ComboBox comboBox)
    {
        return (comboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "=";
    }

    private static double? ParseDouble(TextBox textBox)
    {
        return double.TryParse(textBox.Text, out var value) ? value : null;
    }

    private static void ReplaceSelection<T>(
        ICollection<T> destination,
        params (CheckBox CheckBox, T Value)[] selections)
    {
        destination.Clear();
        foreach (var selection in selections)
        {
            if (selection.CheckBox.IsChecked == true)
                destination.Add(selection.Value);
        }
    }
}
