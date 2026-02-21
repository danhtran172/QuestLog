using MasterPlay.UI.ViewModels;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MasterPlay.UI
{
    public partial class AddGameWindow : Window
    {
        public AddGameWindow()
        {
            InitializeComponent();

            var vm = new AddGameViewModel();
            DataContext = vm;

            vm.RequestClose += result =>
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