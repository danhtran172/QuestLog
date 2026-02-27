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

        // Handler cho GotFocus của TextBox để bôi đen text
        private void SelectAllText(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                textBox.SelectAll();
            }
        }

        // Series
        private void SeriesItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is string seriesName)
            {
                if (DataContext is ViewModels.EditGameViewModel vm)
                {
                    vm.SelectSeriesCommand.Execute(seriesName);
                }
            }
        }

        private void CreateNewSeries_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ViewModels.EditGameViewModel vm)
            {
                vm.CreateNewSeriesCommand.Execute(null);
            }
        }

        // Genre
        private void GenreItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is string genreName)
            {
                if (DataContext is ViewModels.EditGameViewModel vm)
                {
                    vm.SelectGenreCommand.Execute(genreName);
                }
            }
        }

        private void CreateNewGenre_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ViewModels.EditGameViewModel vm)
            {
                vm.CreateNewGenreCommand.Execute(null);
            }
        }

        // Theme
        private void ThemeItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is string themeName)
            {
                if (DataContext is ViewModels.EditGameViewModel vm)
                {
                    vm.SelectThemeCommand.Execute(themeName);
                }
            }
        }

        private void CreateNewTheme_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ViewModels.EditGameViewModel vm)
            {
                vm.CreateNewThemeCommand.Execute(null);
            }
        }

        private void FinishedIn_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            string text = e.Text;
            if (!Regex.IsMatch(text, @"^[0-9,]+$"))
            {
                e.Handled = true;
                return;
            }
        }

        
    }
}