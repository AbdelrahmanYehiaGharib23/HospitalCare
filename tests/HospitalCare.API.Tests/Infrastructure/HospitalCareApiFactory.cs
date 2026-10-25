using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Infrastructure.Persistence.DbInitializer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog;

namespace HospitalCare.API.Tests.Infrastructure;

internal class HospitalCareApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly Dictionary<string, string?> _previousEnvironmentValues = new();
    private readonly TimeProvider? _timeProvider;
    private bool _disposed;

    public string LogDirectory { get; } = Path.Combine(
        Path.GetTempPath(),
        "HospitalCareApiTests",
        Guid.NewGuid().ToString("N"));

    public bool PreserveLogFilesOnDispose { get; set; }

    public HospitalCareApiFactory(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider;
        Directory.CreateDirectory(LogDirectory);
        SetEnvironmentValue("ConnectionStrings__DefaultConnection", "Data Source=:memory:");
        SetEnvironmentValue(
            "Jwt__SecretKey",
            "HospitalCare integration test signing key with enough length");
        SetEnvironmentValue("Jwt__Issuer", "HospitalCare.API.Tests");
        SetEnvironmentValue("Jwt__Audience", "HospitalCare.API.Tests");
        SetEnvironmentValue(
            "PaymentGateway__Fake__WebhookSecret",
            "HospitalCare integration test webhook signing secret");
        SetEnvironmentValue(
            "Serilog__FilePath",
            Path.Combine(LogDirectory, "hospitalcare-.log"));
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

            if (_timeProvider is not null)
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(_timeProvider);
            }
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        Log.CloseAndFlush();
        _connection.Dispose();
        foreach (var (key, value) in _previousEnvironmentValues)
        {
            Environment.SetEnvironmentVariable(key, value);
        }

        if (PreserveLogFilesOnDispose)
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(LogDirectory))
        {
            File.Delete(file);
        }

        Directory.Delete(LogDirectory);
    }

    private void SetEnvironmentValue(string key, string value)
    {
        _previousEnvironmentValues[key] = Environment.GetEnvironmentVariable(key);
        Environment.SetEnvironmentVariable(key, value);
    }

    private sealed class EnsureCreatedDbInitializer : IDbInitializer
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly AppointmentTypeSeeder _appointmentTypeSeeder;

        public EnsureCreatedDbInitializer(
            ApplicationDbContext dbContext,
            AppointmentTypeSeeder appointmentTypeSeeder)
        {
            _dbContext = dbContext;
            _appointmentTypeSeeder = appointmentTypeSeeder;
        }

        public void Initialize()
        {
            _dbContext.Database.EnsureCreated();
            _appointmentTypeSeeder
                .SeedAsync(_dbContext)
                .GetAwaiter()
                .GetResult();
        }
    }
}
