using MasterPlay.Models;
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
                if (result == true) // Chỉ reload khi Save/Delete thành công
                {
                    if (Owner is MainWindow mainWindow)
                    {
                        mainWindow.LoadGamesData();
                    }
                }
                DialogResult = result;
                Close();
            };
        }
    }
}