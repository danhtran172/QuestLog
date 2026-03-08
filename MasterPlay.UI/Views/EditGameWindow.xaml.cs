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
            if (!Regex.IsMatch(e.Text, @"^[0-9.,]$"))
            {
                e.Handled = true;
                return;
            }

            if (e.Text == "." || e.Text == ",")
            {
                if (tb.Text.Contains(".") || tb.Text.Contains(","))
                {
                    e.Handled = true;
                }
            }
        }

        private void FinishedIn_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!Regex.IsMatch(e.Text, @"^[0-9]+$"))
            {
                e.Handled = true;
            }
        }

        private void FinishScore_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            TextBox tb = sender as TextBox;
            if (!Regex.IsMatch(e.Text, @"^[0-9.,]$"))
            {
                e.Handled = true;
                return;
            }

            if (e.Text == "." || e.Text == ",")
            {
                if (tb.Text.Contains(".") || tb.Text.Contains(","))
                {
                    e.Handled = true;
                }
            }
        }

        // ==================== POPUP CLOSED HANDLERS ====================
        private void SeriesPopup_Closed(object sender, EventArgs e)
        {
            if (DataContext is EditGameViewModel vm)
            {
                vm.CategoryManager?.ResetEditing();
            }
        }

        private void GenresPopup_Closed(object sender, EventArgs e)
        {
            if (DataContext is EditGameViewModel vm)
            {
                vm.CategoryManager?.ResetEditing();
            }
        }

        private void ThemesPopup_Closed(object sender, EventArgs e)
        {
            if (DataContext is EditGameViewModel vm)
            {
                vm.CategoryManager?.ResetEditing();
            }
        }

        // ==================== ITEM CLICK HANDLERS ====================
        private void SeriesItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is Series series && DataContext is EditGameViewModel vm)
            {
                vm.SelectSeriesCommand.Execute(series);
            }
        }

        private void CreateNewSeries_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is EditGameViewModel vm)
            {
                vm.CreateNewSeriesCommand.Execute(null);
            }
        }

        private void GenreItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is Genre genre && DataContext is EditGameViewModel vm)
            {
                vm.SelectGenreCommand.Execute(genre);
            }
        }

        private void CreateNewGenre_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is EditGameViewModel vm)
            {
                vm.CreateNewGenreCommand.Execute(null);
            }
        }

        private void ThemeItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is Theme theme && DataContext is EditGameViewModel vm)
            {
                vm.SelectThemeCommand.Execute(theme);
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