using MasterPlay.UI.Services.Windowing;
using System.Windows;
using System.Windows.Input;

namespace MasterPlay.UI.Features.Settings
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();

            var viewModel = (SettingsViewModel)DataContext;
            DataContext = viewModel;
            viewModel.RequestClose += result =>
            {
                DialogResult = result;
                Close();
            };
            Closed += (_, _) =>
            {
                viewModel.RestoreLanguage();
                viewModel.RestoreTheme();
            };
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape || Keyboard.Modifiers != ModifierKeys.None)
                return;

            if (PopupKeyboardService.CloseOpenComboBox(this))
            {
                e.Handled = true;
                return;
            }

            Close();
            e.Handled = true;
        }
    }
}
