using HospitalCare.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HospitalCare.Infrastructure.Persistence.DbInitializer
{
    public class DbInitializer : IDbInitializer
    {
        private readonly ApplicationDbContext _dbContext;

        public DbInitializer(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public void Initialize()
        {
            if (_dbContext.Database.GetPendingMigrations().Any())
            {
                _dbContext.Database.Migrate();
            }
        }
    }
}
