using MasterPlay.Models.Models;

namespace MasterPlay.UI.Services;

public interface IMainWindowActions
{
    bool ShowGameEditor(Game? game);
    bool ShowSettings();
    void LaunchGame(Game game);
}
