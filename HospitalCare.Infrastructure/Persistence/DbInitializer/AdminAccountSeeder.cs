using HospitalCare.Application.Common.Constants;
using HospitalCare.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HospitalCare.Infrastructure.Persistence.DbInitializer;

public sealed class AdminAccountSeeder
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly AdminAccountOptions _options;

    public AdminAccountSeeder(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<AdminAccountOptions> options)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _roleManager = roleManager;
        _options = options.Value;
    }

    public async Task SeedAsync(
        CancellationToken cancellationToken = default)
    {
        var email = _options.Email?.Trim();
        var password = _options.Password;

        if (string.IsNullOrWhiteSpace(email) &&
            string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "SeedAdmin:Email and SeedAdmin:Password must both be configured.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        try
        {
            var existingUser = await _userManager.FindByEmailAsync(email);

            if (existingUser is not null)
            {
                if (await _userManager.IsInRoleAsync(
                        existingUser,
                        Roles.Admin))
                {
                    await transaction.CommitAsync(cancellationToken);
                    return;
                }

                throw new InvalidOperationException(
                    "SeedAdmin email belongs to an account without the Admin role.");
            }

            if (!await _roleManager.RoleExistsAsync(Roles.Admin))
            {
                var roleResult = await _roleManager.CreateAsync(
                    new IdentityRole(Roles.Admin));

                if (!roleResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        FormatIdentityErrors(roleResult.Errors));
                }
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email
            };
            var createResult = await _userManager.CreateAsync(user, password);

            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    FormatIdentityErrors(createResult.Errors));
            }

            var roleAssignmentResult = await _userManager.AddToRoleAsync(
                user,
                Roles.Admin);

            if (!roleAssignmentResult.Succeeded)
            {
                throw new InvalidOperationException(
                    FormatIdentityErrors(roleAssignmentResult.Errors));
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static string FormatIdentityErrors(
        IEnumerable<IdentityError> errors)
    {
        return string.Join("; ", errors.Select(error => error.Description));
    }
}
