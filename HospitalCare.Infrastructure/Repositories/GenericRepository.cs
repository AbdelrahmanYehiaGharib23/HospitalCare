using System.Linq.Expressions;
using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Domain.Entities;
using HospitalCare.Infrastructure.Persistence.DbInitializer;
using Microsoft.EntityFrameworkCore;

namespace HospitalCare.Infrastructure.Repositories
{
    public class GenericRepository<TEntity> : IGenericRepository<TEntity>
     where TEntity : BaseEntity
    {
        private readonly ApplicationDbContext _dbContext;

        public GenericRepository(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<TEntity?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            return await _dbContext.Set<TEntity>()
                .FindAsync(new object[] { id }, cancellationToken);
        }

        public void Add(TEntity entity)
            => _dbContext.Set<TEntity>().Add(entity);

        public async Task<IEnumerable<TEntity>> GetAllAsync(
            bool withTracking = false,
            CancellationToken cancellationToken = default)
        {
            if (withTracking)
            {
                return await _dbContext.Set<TEntity>()
                    .ToListAsync(cancellationToken);
            }

            return await _dbContext.Set<TEntity>()
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }

        public void Remove(TEntity entity)
        {
            _dbContext.Set<TEntity>().Remove(entity);
        }

        public void Update(TEntity entity)
            => _dbContext.Set<TEntity>().Update(entity);

        public async Task<IEnumerable<TEntity>> FindAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            return await _dbContext.Set<TEntity>()
                .Where(predicate)
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }
    }
}
