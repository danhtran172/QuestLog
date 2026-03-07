using MasterPlay.Models.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace MasterPlay.Models.Repositories
{
    public interface IGamePositionRepository : IGenericRepository<GamePosition>
    {
        void DeleteAll();
        void AddRange(IEnumerable<GamePosition> positions);
    }

    public class GamePositionRepository : GenericRepository<GamePosition>, IGamePositionRepository
    {
        public GamePositionRepository(MasterPlayContext context) : base(context)
        {
        }

        public void DeleteAll()
        {
            _dbSet.RemoveRange(_dbSet);
        }

        public void AddRange(IEnumerable<GamePosition> positions)
        {
            _dbSet.AddRange(positions);
        }
    }
}