using HospitalCare.Application.Common.Constants;
using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HospitalCare.Infrastructure.Persistence.DbInitializer;

public class DbInitializer : IDbInitializer
{
    private static readonly string[] RequiredRoles =
    [
        Roles.Admin,
        Roles.Doctor,
        Roles.Receptionist,
        Roles.Patient
    ];

    private readonly ApplicationDbContext _dbContext;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly AdminAccountSeeder _adminAccountSeeder;
    private readonly AppointmentTypeSeeder _appointmentTypeSeeder;

    public DbInitializer(
        ApplicationDbContext dbContext,
        RoleManager<IdentityRole> roleManager,
        AdminAccountSeeder adminAccountSeeder,
        AppointmentTypeSeeder appointmentTypeSeeder)
    {
        _dbContext = dbContext;
        _roleManager = roleManager;
        _adminAccountSeeder = adminAccountSeeder;
        _appointmentTypeSeeder = appointmentTypeSeeder;
    }

    public void Initialize()
    {
        if (_dbContext.Database.GetPendingMigrations().Any())
        {
            _dbContext.Database.Migrate();
        }

        SeedRolesAsync().GetAwaiter().GetResult();
        _adminAccountSeeder.SeedAsync().GetAwaiter().GetResult();
        _appointmentTypeSeeder
            .SeedAsync(_dbContext)
            .GetAwaiter()
            .GetResult();
    }

    private async Task SeedRolesAsync()
    {
        foreach (var roleName in RequiredRoles)
        {
            if (await _roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var result = await _roleManager.CreateAsync(
                new IdentityRole(roleName));

            if (!result.Succeeded)
            {
                var errors = string.Join("; ",result.Errors.Select(error => error.Description));

                throw new InvalidOperationException(
                    $"Failed to seed role '{roleName}': {errors}");
            }
        }
    }
}
