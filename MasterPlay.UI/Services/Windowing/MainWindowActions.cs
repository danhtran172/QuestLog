using MasterPlay.Core.Localization;
using MasterPlay.Infrastructure.FileSystem;
using MasterPlay.Core.Entities;
using MasterPlay.UI.Features.Library;
using MasterPlay.UI.Features.GameEditor;
using MasterPlay.UI.Features.Settings;
using System.Windows;

namespace MasterPlay.UI.Services.Windowing;

public sealed class MainWindowActions : IMainWindowActions
{
    public bool ShowGameEditor(Game? game)
    {
        var window = new GameEditorWindow(game)
        {
            Owner = Application.Current.MainWindow
        };
        window.ShowDialog();
        return true;
    }

    public bool ShowSettings()
    {
        var window = new SettingsWindow
        {
            Owner = Application.Current.MainWindow
        };
        window.ShowDialog();
        return true;
    }

    public void LaunchGame(Game game)
    {
        var processPath = new GameViewModel(game).GetLaunchProcessPath();
        if (string.IsNullOrWhiteSpace(processPath))
        {
            MessageBox.Show(
                LocalizationService.Get("GameExecutableNotFound"),
                LocalizationService.Get("Notification"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        FileHelper.RunProcess(processPath);
    }
}
