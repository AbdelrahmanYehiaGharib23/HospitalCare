using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Infrastructure.Persistence.DbInitializer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HospitalCare.API.Tests.Infrastructure;

internal sealed class HospitalCareApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly Dictionary<string, string?> _previousEnvironmentValues = new();

    public HospitalCareApiFactory()
    {
        SetEnvironmentValue("ConnectionStrings__DefaultConnection", "Data Source=:memory:");
        SetEnvironmentValue(
            "Jwt__SecretKey",
            "HospitalCare integration test signing key with enough length");
        SetEnvironmentValue("Jwt__Issuer", "HospitalCare.API.Tests");
        SetEnvironmentValue("Jwt__Audience", "HospitalCare.API.Tests");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(_connection));

            services.RemoveAll<IDbInitializer>();
            services.AddScoped<IDbInitializer, EnsureCreatedDbInitializer>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection.Dispose();
            foreach (var (key, value) in _previousEnvironmentValues)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }

    private void SetEnvironmentValue(string key, string value)
    {
        _previousEnvironmentValues[key] = Environment.GetEnvironmentVariable(key);
        Environment.SetEnvironmentVariable(key, value);
    }

    private sealed class EnsureCreatedDbInitializer : IDbInitializer
    {
        private readonly ApplicationDbContext _dbContext;

        public EnsureCreatedDbInitializer(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public void Initialize()
        {
            _dbContext.Database.EnsureCreated();
        }
    }
}
