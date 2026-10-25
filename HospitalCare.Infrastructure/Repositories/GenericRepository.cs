using System.Linq.Expressions;
using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Application.Common.Models;
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

        public Task<PagedResult<TEntity>> GetPageAsync(
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            return GetPageAsync(
                _dbContext.Set<TEntity>(),
                pageNumber,
                pageSize,
                cancellationToken);
        }

        public Task<PagedResult<TEntity>> GetPageAsync(
            Expression<Func<TEntity, bool>> predicate,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            return GetPageAsync(
                _dbContext.Set<TEntity>().Where(predicate),
                pageNumber,
                pageSize,
                cancellationToken);
        }

        private static async Task<PagedResult<TEntity>> GetPageAsync(
            IQueryable<TEntity> source,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken)
        {
            var query = source.AsNoTracking();
            var totalCount = await query.CountAsync(cancellationToken);
            var skip = (long)(pageNumber - 1) * pageSize;

            if (skip >= totalCount)
            {
                return new PagedResult<TEntity>(
                    [],
                    pageNumber,
                    pageSize,
                    totalCount);
            }

            var items = await query
                .OrderBy(entity => entity.Id)
                .Skip((int)skip)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return new PagedResult<TEntity>(
                items,
                pageNumber,
                pageSize,
                totalCount);
        }

        public void Remove(TEntity entity)
        {
            _dbContext.Set<TEntity>().Remove(entity);
        }

        public void Update(TEntity entity)
            => _dbContext.Set<TEntity>().Update(entity);

        public async Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate,bool withTracking = false,CancellationToken cancellationToken = default)
        {
            IQueryable<TEntity> query = _dbContext
                .Set<TEntity>()
                .Where(predicate);

            if (!withTracking)
            {
                query = query.AsNoTracking();
            }

            return await query.ToListAsync(cancellationToken);
        }
    }
}
