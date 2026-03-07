using MasterPlay.Models.Models;
using MasterPlay.Models.Repositories;

namespace MasterPlay.Models
{
    public interface IUnitOfWork : System.IDisposable
    {
        IGameRepository Games { get; }
        IGenericRepository<Series> Series { get; }
        IGenericRepository<Genre> Genres { get; }
        IGenericRepository<Theme> Themes { get; }
        IGenericRepository<ProgressStatus> ProgressStatuses { get; }
        IGenericRepository<OwnershipStatus> OwnershipStatuses { get; }
        IGamePositionRepository GamePositions { get; }
        IGenericRepository<DetectedProcess> DetectedProcesses { get; }
        IGenericRepository<GameGenre> GameGenres { get; }
        IGenericRepository<GameTheme> GameThemes { get; }

        int Complete();
    }

    public class UnitOfWork : IUnitOfWork
    {
        private readonly MasterPlayContext _context;

        public UnitOfWork(MasterPlayContext context)
        {
            _context = context;
            Games = new GameRepository(_context);
            Series = new GenericRepository<Series>(_context);
            Genres = new GenericRepository<Genre>(_context);
            Themes = new GenericRepository<Theme>(_context);
            ProgressStatuses = new GenericRepository<ProgressStatus>(_context);
            OwnershipStatuses = new GenericRepository<OwnershipStatus>(_context);
            GamePositions = new GamePositionRepository(_context);
            DetectedProcesses = new GenericRepository<DetectedProcess>(_context);
            GameGenres = new GenericRepository<GameGenre>(_context);
            GameThemes = new GenericRepository<GameTheme>(_context);
        }

        public IGameRepository Games { get; private set; }
        public IGenericRepository<Series> Series { get; private set; }
        public IGenericRepository<Genre> Genres { get; private set; }
        public IGenericRepository<Theme> Themes { get; private set; }
        public IGenericRepository<ProgressStatus> ProgressStatuses { get; private set; }
        public IGenericRepository<OwnershipStatus> OwnershipStatuses { get; private set; }
        public IGamePositionRepository GamePositions { get; private set; }
        public IGenericRepository<DetectedProcess> DetectedProcesses { get; private set; }
        public IGenericRepository<GameGenre> GameGenres { get; private set; }
        public IGenericRepository<GameTheme> GameThemes { get; private set; }

        public int Complete() => _context.SaveChanges();
        public void Dispose() => _context.Dispose();
    }
}