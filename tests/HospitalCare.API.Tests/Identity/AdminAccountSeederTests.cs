using HospitalCare.API.Tests.Infrastructure;
using HospitalCare.Application.Common.Constants;
using HospitalCare.Infrastructure.Identity;
using HospitalCare.Infrastructure.Persistence.DbInitializer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HospitalCare.API.Tests.Identity;

public sealed class AdminAccountSeederTests
{
    [Fact]
    public async Task SeedAsync_ConfiguredCredentials_CreatesAdminOnlyOnce()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var seeder = CreateSeeder(
            services,
            new AdminAccountOptions
            {
                Email = "admin@example.com",
                Password = "Password123!"
            });

        await seeder.SeedAsync(TestContext.Current.CancellationToken);
        await seeder.SeedAsync(TestContext.Current.CancellationToken);

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync("admin@example.com");

        Assert.NotNull(user);
        Assert.Contains(Roles.Admin, await userManager.GetRolesAsync(user));
        Assert.Equal(1, await userManager.Users.CountAsync(
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SeedAsync_UnconfiguredCredentials_SkipsAdminCreation()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var seeder = CreateSeeder(services, new AdminAccountOptions());

        await seeder.SeedAsync(TestContext.Current.CancellationToken);

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Empty(await userManager.Users.ToListAsync(
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SeedAsync_PartialCredentials_ThrowsConfigurationError()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var seeder = CreateSeeder(
            scope.ServiceProvider,
            new AdminAccountOptions { Email = "admin@example.com" });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => seeder.SeedAsync(TestContext.Current.CancellationToken));

        Assert.Equal(
            "SeedAdmin:Email and SeedAdmin:Password must both be configured.",
            exception.Message);
    }

    [Fact]
    public async Task SeedAsync_EmailBelongsToNonAdminAccount_DoesNotPromoteAccount()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var email = "receptionist@example.com";
        var user = new ApplicationUser { UserName = email, Email = email };

        Assert.True((await userManager.CreateAsync(user, "Password123!")).Succeeded);
        Assert.True((await roleManager.CreateAsync(
            new IdentityRole(Roles.Receptionist))).Succeeded);
        Assert.True((await userManager.AddToRoleAsync(
            user,
            Roles.Receptionist)).Succeeded);

        var seeder = CreateSeeder(
            services,
            new AdminAccountOptions
            {
                Email = email,
                Password = "Password123!"
            });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => seeder.SeedAsync(TestContext.Current.CancellationToken));

        var roles = await userManager.GetRolesAsync(user);
        Assert.Contains(Roles.Receptionist, roles);
        Assert.DoesNotContain(Roles.Admin, roles);
    }

    private static AdminAccountSeeder CreateSeeder(
        IServiceProvider services,
        AdminAccountOptions options)
    {
        return new AdminAccountSeeder(
            services.GetRequiredService<ApplicationDbContext>(),
            services.GetRequiredService<UserManager<ApplicationUser>>(),
            services.GetRequiredService<RoleManager<IdentityRole>>(),
            Options.Create(options));
    }
}
