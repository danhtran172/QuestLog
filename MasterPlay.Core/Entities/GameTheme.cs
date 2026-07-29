using MasterPlay.Models.Models;

public class GameTheme
{
    public string GameId { get; set; } = string.Empty;
    public int ThemeId { get; set; }
    public virtual Game Game { get; set; } = null!;
    public virtual Theme Theme { get; set; } = null!;
}
