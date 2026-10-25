using HospitalCare.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HospitalCare.Infrastructure.Persistence.DbInitializer;

public sealed class AppointmentTypeSeeder
{
    private static readonly AppointmentType[] Defaults =
    [
        new()
        {
            Name = "Consultation",
            DurationMinutes = 30,
            BookingFee = 100m
        },
        new()
        {
            Name = "Follow-up",
            DurationMinutes = 15,
            BookingFee = 50m
        }
    ];

    public async Task SeedAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var existingNames = (await dbContext.AppointmentTypes
                .AsNoTracking()
                .Select(appointmentType => appointmentType.Name)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var appointmentType in Defaults)
        {
            if (!existingNames.Add(appointmentType.Name))
            {
                continue;
            }

            dbContext.AppointmentTypes.Add(
                new AppointmentType
                {
                    Name = appointmentType.Name,
                    DurationMinutes = appointmentType.DurationMinutes,
                    BookingFee = appointmentType.BookingFee
                });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
