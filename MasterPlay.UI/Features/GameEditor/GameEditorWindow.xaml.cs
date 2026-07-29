using MasterPlay.Core.Entities;
using MasterPlay.UI.Features.Library;
using MasterPlay.UI.Features.GameEditor;
using MasterPlay.UI.Features.Settings;
using System.Windows;

namespace MasterPlay.UI.Features.GameEditor;

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
