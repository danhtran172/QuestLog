using MasterPlay.Models.Models;

public class GameGenre
{
    public string GameId { get; set; } = string.Empty;
    public int GenreId { get; set; }
    public virtual Game Game { get; set; } = null!;
    public virtual Genre Genre { get; set; } = null!;
}
