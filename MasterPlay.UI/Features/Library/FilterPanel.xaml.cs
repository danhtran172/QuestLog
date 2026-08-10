using MasterPlay.Core.Entities;
using MasterPlay.UI.Shared.ViewModels;
using System.Collections;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace MasterPlay.UI.Features.Library;

public partial class FilterPanel : UserControl
{
    public event EventHandler? CloseRequested;

    private ICollectionView? _genreOptionsView;
    private ICollectionView? _themeOptionsView;

    public FilterPanel()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => ConfigureTagOptionViews();
    }

    private void FilterPanel_Loaded(object sender, RoutedEventArgs e)
    {
        ConfigureTagOptionViews();
    }

    public void FocusSearchBox()
    {
        GenreSearchBox.Focus();
        GenreSearchBox.SelectAll();
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
        GenreSearchBox.Clear();
        ThemeSearchBox.Clear();

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

        filter.SelectedOwnershipIds.Clear();
        foreach (var checkBox in GetOwnershipCheckBoxes())
        {
            if (checkBox.IsChecked == true && checkBox.Tag is int id)
                filter.SelectedOwnershipIds.Add(id);
        }

        UpdateSelectionFromVisibleCheckBoxes(filter.SelectedGenreIds, GetGenreCheckBoxes());
        UpdateSelectionFromVisibleCheckBoxes(filter.SelectedThemeIds, GetThemeCheckBoxes());

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

    private void GenreSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _genreOptionsView?.Refresh();
    }

    private void ThemeSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _themeOptionsView?.Refresh();
    }

    private void ConfigureTagOptionViews()
    {
        _genreOptionsView = CreateFilteredView(GenreOptionsList.ItemsSource, MatchesGenreSearch);
        _themeOptionsView = CreateFilteredView(ThemeOptionsList.ItemsSource, MatchesThemeSearch);
    }

    private static ICollectionView? CreateFilteredView(IEnumerable? source, Predicate<object> filter)
    {
        if (source == null)
            return null;

        var view = CollectionViewSource.GetDefaultView(source);
        view.Filter = filter;
        return view;
    }

    private bool MatchesGenreSearch(object item) =>
        item is Genre genre && MatchesSearchText(genre.Name, GenreSearchBox.Text);

    private bool MatchesThemeSearch(object item) =>
        item is Theme theme && MatchesSearchText(theme.Name, ThemeSearchBox.Text);

    private static bool MatchesSearchText(string? value, string searchText) =>
        string.IsNullOrWhiteSpace(searchText)
        || (value?.Contains(searchText.Trim(), StringComparison.OrdinalIgnoreCase) ?? false);

    private IEnumerable<CheckBox> GetFilterCheckBoxes()
    {
        var checkBoxes = new List<CheckBox>
        {
            AvgVeryLong, AvgLong, AvgMedium, AvgShort, AvgVeryShort, AvgNone,
            ProgressNextPlay, ProgressDone, ProgressPlaying, ProgressIntended, ProgressDrop, ProgressPending,
            MetaVeryHigh, MetaHigh, MetaMedium, MetaLow, MetaVeryLow, MetaNone
        };
        checkBoxes.AddRange(GetOwnershipCheckBoxes());
        checkBoxes.AddRange(GetGenreCheckBoxes());
        checkBoxes.AddRange(GetThemeCheckBoxes());
        return checkBoxes;
    }

    private IEnumerable<CheckBox> GetOwnershipCheckBoxes() =>
        FindVisualChildren<CheckBox>(OwnershipOptionsList);

    private IEnumerable<CheckBox> GetGenreCheckBoxes() =>
        FindVisualChildren<CheckBox>(GenreOptionsList);

    private IEnumerable<CheckBox> GetThemeCheckBoxes() =>
        FindVisualChildren<CheckBox>(ThemeOptionsList);

    private void OwnershipCheckBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { Tag: int id } checkBox &&
            DataContext is MainWindowViewModel viewModel)
        {
            checkBox.IsChecked = viewModel.CurrentFilter.SelectedOwnershipIds.Contains(id);
        }
    }

    private void GenreCheckBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { Tag: int id } checkBox &&
            DataContext is MainWindowViewModel viewModel)
        {
            checkBox.IsChecked = viewModel.CurrentFilter.SelectedGenreIds.Contains(id);
        }
    }

    private void ThemeCheckBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { Tag: int id } checkBox &&
            DataContext is MainWindowViewModel viewModel)
        {
            checkBox.IsChecked = viewModel.CurrentFilter.SelectedThemeIds.Contains(id);
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
                yield return match;

            foreach (var descendant in FindVisualChildren<T>(child))
                yield return descendant;
        }
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

    private static void UpdateSelectionFromVisibleCheckBoxes(ICollection<int> destination, IEnumerable<CheckBox> checkBoxes)
    {
        var visibleCheckBoxes = checkBoxes
            .Where(checkBox => checkBox.Tag is int)
            .ToList();

        foreach (var id in visibleCheckBoxes.Select(checkBox => (int)checkBox.Tag))
            destination.Remove(id);

        foreach (var checkBox in visibleCheckBoxes)
        {
            if (checkBox.IsChecked == true && checkBox.Tag is int id)
                destination.Add(id);
        }
    }
}
