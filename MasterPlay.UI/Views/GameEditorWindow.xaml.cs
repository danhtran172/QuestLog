using MasterPlay.Models.Models;
using MasterPlay.UI.ViewModels;
using System.Windows;

namespace MasterPlay.UI.Views;

public partial class GameEditorWindow : Window
{
    public GameEditorWindow(Game? game = null)
    {
        InitializeComponent();
        var viewModel = new GameEditorViewModel(game);
        DataContext = viewModel;
        viewModel.RequestClose += result =>
        {
            DialogResult = result;
            Close();
        };
    }
}
