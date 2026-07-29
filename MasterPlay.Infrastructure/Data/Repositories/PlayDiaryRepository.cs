using MasterPlay.Core.Entities;
using MasterPlay.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MasterPlay.Infrastructure.Data.Repositories
{
    public interface IPlayDiaryRepository : IGenericRepository<PlayDiary>
    {
        IEnumerable<PlayDiary> GetByGame(string gameId);
        IEnumerable<PlayDiary> GetByDateRange(DateTime start, DateTime end);
        int GetTotalTimeByGame(string gameId);
    }

    public class PlayDiaryRepository : GenericRepository<PlayDiary>, IPlayDiaryRepository
    {

        public PlayDiaryRepository(MasterPlayContext context) : base(context) { }


        public IEnumerable<PlayDiary> GetByGame(string gameId)
        {
            return _dbSet.Where(p => p.GameId == gameId)
                         .OrderByDescending(p => p.Date)
                         .ToList();
        }


        public IEnumerable<PlayDiary> GetByDateRange(DateTime start, DateTime end)
        {
            return _dbSet.Where(p => p.Date >= start && p.Date <= end)
                         .OrderByDescending(p => p.Date)
                         .ToList();
        }


        public int GetTotalTimeByGame(string gameId)
        {
            return _dbSet.Where(p => p.GameId == gameId)
                         .Sum(p => p.TimeSpent);
        }
    }
}
