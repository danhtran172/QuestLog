using MasterPlay.Models.Models;

public class GameTheme
{
    public string GameId { get; set; }
    public int ThemeId { get; set; }
    public virtual Game Game { get; set; }
    public virtual Theme Theme { get; set; }
}