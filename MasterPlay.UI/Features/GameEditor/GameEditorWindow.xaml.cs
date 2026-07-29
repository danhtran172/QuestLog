using MasterPlay.Core.Entities;
using MasterPlay.UI.Services.Windowing;
using System.Windows;
using System.Windows.Input;

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

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || Keyboard.Modifiers != ModifierKeys.None)
            return;

        if (DataContext is GameEditorViewModel viewModel && viewModel.CloseTopPopup())
        {
            e.Handled = true;
            return;
        }

        if (PopupKeyboardService.CloseOpenComboBox(this))
        {
            e.Handled = true;
            return;
        }

        Close();
        e.Handled = true;
    }
}
