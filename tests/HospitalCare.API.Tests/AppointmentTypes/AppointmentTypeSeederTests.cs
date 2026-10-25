using HospitalCare.Infrastructure.Persistence.DbInitializer;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HospitalCare.API.Tests.AppointmentTypes;

public sealed class AppointmentTypeSeederTests
{
    [Fact]
    public async Task SeedAsync_CalledTwice_SeedsDefaultsOnlyOnce()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new ApplicationDbContext(options);
        await dbContext.Database.EnsureCreatedAsync(
            TestContext.Current.CancellationToken);

        var seeder = new AppointmentTypeSeeder();
        await seeder.SeedAsync(dbContext, TestContext.Current.CancellationToken);
        await seeder.SeedAsync(dbContext, TestContext.Current.CancellationToken);

        var appointmentTypes = await dbContext.AppointmentTypes
            .AsNoTracking()
            .OrderBy(appointmentType => appointmentType.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Collection(
            appointmentTypes,
            consultation =>
            {
                Assert.Equal("Consultation", consultation.Name);
                Assert.Equal(30, consultation.DurationMinutes);
                Assert.Equal(100m, consultation.BookingFee);
            },
            followUp =>
            {
                Assert.Equal("Follow-up", followUp.Name);
                Assert.Equal(15, followUp.DurationMinutes);
                Assert.Equal(50m, followUp.BookingFee);
            });
    }
}
