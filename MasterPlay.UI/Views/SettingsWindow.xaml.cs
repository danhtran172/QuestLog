using MasterPlay.UI.ViewModels;
using System.Windows;

namespace MasterPlay.UI
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
            Closed += (_, _) => viewModel.RestoreLanguage();
        }
    }
}
