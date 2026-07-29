using MasterPlay.Core.Entities;
using MasterPlay.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace MasterPlay.Infrastructure.Data.Repositories
{
    public interface IGenericRepository<T> where T : class
    {
        T GetById(object id);
        IEnumerable<T> GetAll();
        IEnumerable<T> Find(Expression<Func<T, bool>> predicate);
        void Add(T entity);
        void AddRange(IEnumerable<T> entities);
        void Update(T entity);
        void Remove(T entity);
        void RemoveRange(IEnumerable<T> entities);
    }
    public class GenericRepository<T> : IGenericRepository<T> where T : class
    {
        protected readonly MasterPlayContext _context;
        protected readonly DbSet<T> _dbSet;

        public GenericRepository(MasterPlayContext context)
        {
            _context = context;
            _dbSet = context.Set<T>();
        }

        public T GetById(object id) => _dbSet.Find(id);

        public IEnumerable<T> GetAll() => _dbSet.ToList();

        public IEnumerable<T> Find(Expression<Func<T, bool>> predicate)
            => _dbSet.Where(predicate).ToList();

        public void Add(T entity) => _dbSet.Add(entity);

        public void AddRange(IEnumerable<T> entities) => _dbSet.AddRange(entities);

        public void Update(T entity) => _dbSet.Update(entity);

        public void Remove(T entity) => _dbSet.Remove(entity);

        public void RemoveRange(IEnumerable<T> entities) => _dbSet.RemoveRange(entities);
    }
}
