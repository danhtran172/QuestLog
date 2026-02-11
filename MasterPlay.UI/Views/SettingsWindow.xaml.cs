using MasterPlay.UI.ViewModels;
using System.Windows;

namespace MasterPlay.UI
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            
            var vm = (SettingsViewModel)DataContext;
            vm.RequestClose += res =>
            {
                DialogResult = res;
                Close();
            };
        }
    }
}