using MasterPlay.Models.Models;
using MasterPlay.UI.ViewModels;
using System.Text.RegularExpressions;
using System.Windows;

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
        private void Score_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            string text = e.Text;
            if (!Regex.IsMatch(text, @"^[0-9]+$")) // Chỉ số
            {
                e.Handled = true;
                return;
            }
        }

        private void Excited_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            string text = e.Text;
            if (!Regex.IsMatch(text, @"^[0-9]+$"))
            {
                e.Handled = true;
                return;
            }
        }

        private void FinishedIn_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            string text = e.Text;
            if (!Regex.IsMatch(text, @"^[0-9]+$"))
            {
                e.Handled = true;
                return;
            }
        }
    }
}