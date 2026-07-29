using MasterPlay.Models.Models;
using MasterPlay.Models.Repositories;
using Microsoft.EntityFrameworkCore;

public interface IGameRepository : IGenericRepository<Game>
{
    IEnumerable<Game> GetAllWithDetails();
    IEnumerable<Game> GetActiveGames();
    void UpdateWithDetails(Game game);
    Game GetByIdWithDetails(string id);
}

public class GameRepository : GenericRepository<Game>, IGameRepository
{
    public GameRepository(MasterPlayContext context) : base(context) { }

    public IEnumerable<Game> GetAllWithDetails()
    {
        return _dbSet
            .AsNoTracking()
            .Include(g => g.Series)
            .Include(g => g.Ownership)
            .Include(g => g.Progress)
            .Include(g => g.GameGenres).ThenInclude(gg => gg.Genre)
            .Include(g => g.GameThemes).ThenInclude(gt => gt.Theme)
            .Include(g => g.GamePosition)
            .ToList();
    }

    public IEnumerable<Game> GetActiveGames()
    {
        return _dbSet.AsNoTracking().Where(g => g.Ready && !g.IsDisabled).ToList();
    }

    public Game GetByIdWithDetails(string id)
    {
        return _dbSet
            .AsNoTracking()
            .Include(g => g.Series)
            .Include(g => g.Ownership)
            .Include(g => g.Progress)
            .Include(g => g.GameGenres).ThenInclude(gg => gg.Genre)
            .Include(g => g.GameThemes).ThenInclude(gt => gt.Theme)
            .Include(g => g.GamePosition)
            .FirstOrDefault(g => g.Id == id)!;
    }
    public void UpdateWithDetails(Game game)
    {
        var existing = _dbSet
            .Include(g => g.GameGenres)
            .Include(g => g.GameThemes)
            .Include(g => g.GamePosition)
            .FirstOrDefault(g => g.Id == game.Id);

        if (existing == null) throw new Exception("Game not found");

        _context.Entry(existing).CurrentValues.SetValues(game);

        existing.SeriesId = game.SeriesId;

        existing.GameGenres.Clear();
        foreach (var gg in game.GameGenres)
        {
            existing.GameGenres.Add(new GameGenre { GameId = game.Id, GenreId = gg.GenreId });
        }

        existing.GameThemes.Clear();
        foreach (var gt in game.GameThemes)
        {
            existing.GameThemes.Add(new GameTheme { GameId = game.Id, ThemeId = gt.ThemeId });
        }

        if (game.GamePosition != null)
        {
            if (existing.GamePosition == null)
                existing.GamePosition = new GamePosition { GameId = game.Id };
            existing.GamePosition.PlayOrder = game.GamePosition.PlayOrder;
        }
        else
        {
            if (existing.GamePosition != null)
                _context.Remove(existing.GamePosition);
        }
    }
}
