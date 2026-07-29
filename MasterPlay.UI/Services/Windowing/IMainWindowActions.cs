using MasterPlay.Core.Entities;

namespace MasterPlay.UI.Services.Windowing;

public interface IMainWindowActions
{
    bool ShowGameEditor(Game? game);
    bool ShowSettings();
    void LaunchGame(Game game);
}
