
using System.Linq.Expressions;
using HospitalCare.Domain.Entities;

namespace HospitalCare.Application.Common.Interfaces
{
    public interface IGenericRepository<TEntity>
       where TEntity : BaseEntity
    {
        Task<TEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

        void Add(TEntity entity);

        Task<IEnumerable<TEntity>> GetAllAsync(bool withTracking = false, CancellationToken cancellationToken = default);

        void Remove(TEntity entity);

        void Update(TEntity entity);

        Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate, bool withTracking = false , CancellationToken cancellationToken = default);
        
    }
}
