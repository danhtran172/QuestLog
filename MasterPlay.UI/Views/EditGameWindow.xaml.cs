using MasterPlay.Models.Models;
using MasterPlay.UI.ViewModels;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MasterPlay.UI
{
    public partial class EditGameWindow : Window
    {
        public EditGameWindow(Game game)
        {
            InitializeComponent();

            var viewModel = new EditGameViewModel(game);
            DataContext = viewModel;
            viewModel.RequestClose += result =>
            {
                DialogResult = result;
                Close();
            };
        }

        // ==================== INPUT VALIDATION ====================
        private void Score_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            TextBox tb = sender as TextBox;
            if (!Regex.IsMatch(e.Text, @"^[0-9.]$"))
            {
                e.Handled = true;
                return;
            }

            if (e.Text == "." && tb.Text.Contains("."))
            {
                e.Handled = true;
            }
        }

        private void FinishedIn_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!Regex.IsMatch(e.Text, @"^[0-9,]+$"))
            {
                e.Handled = true;
            }
        }

        // ==================== SERIES EVENT HANDLERS ====================
        private void SeriesItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is string seriesName)
            {
                if (DataContext is EditGameViewModel vm)
                {
                    vm.SelectSeriesCommand.Execute(seriesName);
                }
            }
        }

        private void CreateNewSeries_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is EditGameViewModel vm)
            {
                vm.CreateNewSeriesCommand.Execute(null);
            }
        }

        // ==================== GENRE EVENT HANDLERS ====================
        private void GenreItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is string genreName)
            {
                if (DataContext is EditGameViewModel vm)
                {
                    vm.SelectGenreCommand.Execute(genreName);
                }
            }
        }

        private void CreateNewGenre_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is EditGameViewModel vm)
            {
                vm.CreateNewGenreCommand.Execute(null);
            }
        }

        // ==================== THEME EVENT HANDLERS ====================
        private void ThemeItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is string themeName)
            {
                if (DataContext is EditGameViewModel vm)
                {
                    vm.SelectThemeCommand.Execute(themeName);
                }
            }
        }

        private void CreateNewTheme_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is EditGameViewModel vm)
            {
                vm.CreateNewThemeCommand.Execute(null);
            }
        }
    }
}