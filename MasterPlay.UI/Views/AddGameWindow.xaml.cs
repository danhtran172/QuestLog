using MasterPlay.UI.ViewModels;
using System.Windows;

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
    }
}