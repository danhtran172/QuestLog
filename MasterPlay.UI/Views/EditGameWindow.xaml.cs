using MasterPlay.Models.Models;
using MasterPlay.UI.ViewModels;
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
    }
}