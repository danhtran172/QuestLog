using MasterPlay.Models.Models;

public class GameGenre
{
    public string GameId { get; set; }
    public int GenreId { get; set; }
    public virtual Game Game { get; set; }
    public virtual Genre Genre { get; set; }
}