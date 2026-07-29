using MasterPlay.UI.Features.Library;
using MasterPlay.UI.Features.GameEditor;
using MasterPlay.UI.Features.Settings;
using System.Windows;

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
            Closed += (_, _) => viewModel.RestoreLanguage();
        }
    }
}
