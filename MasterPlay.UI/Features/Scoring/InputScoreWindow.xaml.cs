using System;
using System.Windows;
using MasterPlay.Core.Localization;
using System.Windows.Controls;
using System.Windows.Input;

namespace MasterPlay.UI.Features.Scoring
{
    public partial class InputScoreWindow : Window
    {
        public string GameName { get; set; }
        public string ScoreInput { get; set; } = "";

        public InputScoreWindow(string gameName)
        {
            InitializeComponent();
            GameName = gameName;
            DataContext = this;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            string input = ScoreInput.Trim();
            if (input == "999" || (float.TryParse(input, out float score) && score >= 0 && score <= 10))
            {
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show(LocalizationService.Get("ScoreInputRange"), LocalizationService.Get("InvalidScoreTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            string s = e.Text;
            if (!char.IsDigit(s[0]) && s != ".") e.Handled = true;
            if (s == "." && ((TextBox)sender).Text.Contains(".")) e.Handled = true;
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }
    }
}
