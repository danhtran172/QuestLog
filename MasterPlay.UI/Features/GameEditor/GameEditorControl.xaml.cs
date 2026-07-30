using MasterPlay.Core.Entities;
using MasterPlay.UI.Features.Library;
using MasterPlay.UI.Features.GameEditor;
using MasterPlay.UI.Features.Settings;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MasterPlay.UI.Features.GameEditor
{
    public partial class GameEditorControl : UserControl
    {
        public GameEditorControl()
        {
            InitializeComponent();
            PopupBackdrop.MouseLeftButtonDown += PopupBackdrop_MouseLeftButtonDown;
        }


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



        private void SeriesItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is Series series && DataContext is GameEditorViewModel vm)
            {
                vm.SelectSeriesCommand.Execute(series);
            }
        }

        private void CreateNewSeries_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is GameEditorViewModel vm)
            {
                vm.CreateNewSeriesCommand.Execute(null);
            }
        }

        private void GenreItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is Genre genre && DataContext is GameEditorViewModel vm)
            {
                vm.SelectGenreCommand.Execute(genre);
            }
        }

        private void CreateNewGenre_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is GameEditorViewModel vm)
            {
                vm.CreateNewGenreCommand.Execute(null);
            }
        }

        private void ThemeItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is Theme theme && DataContext is GameEditorViewModel vm)
            {
                vm.SelectThemeCommand.Execute(theme);
            }
        }

        private void CreateNewTheme_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is GameEditorViewModel vm)
            {
                vm.CreateNewThemeCommand.Execute(null);
            }
        }

        private void Popup_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape || Keyboard.Modifiers != ModifierKeys.None)
                return;

            if (DataContext is GameEditorViewModel viewModel && viewModel.CloseTopPopup())
                e.Handled = true;
        }

        private void PopupBackdrop_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is GameEditorViewModel viewModel && viewModel.CloseTopPopup())
                e.Handled = true;
        }
    }
}
