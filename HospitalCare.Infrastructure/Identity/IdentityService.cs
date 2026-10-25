using HospitalCare.Application.Common.Exceptions;
using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Application.Common.Models;
using HospitalCare.Domain.Entities;
using HospitalCare.Infrastructure.Persistence.DbInitializer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HospitalCare.Infrastructure.Identity;

public class IdentityService : IIdentityService
{
    private const string PatientRole = "Patient";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ApplicationDbContext _dbContext;

    public IdentityService(UserManager<ApplicationUser> userManager,RoleManager<IdentityRole> roleManager,
        IJwtTokenService jwtTokenService,
        IUnitOfWork unitOfWork,
        ApplicationDbContext dbContext)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _jwtTokenService = jwtTokenService;
        _unitOfWork = unitOfWork;
        _dbContext = dbContext;
    }

    public async Task<JwtTokenResult> RegisterAsync(
        string name,
        string email,
        string password,
        string phone,
        DateOnly dateOfBirth,
        string? allergies = null,
        string? chronicConditions = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        try
        {
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email
            };

            var createResult =
                await _userManager.CreateAsync(user, password);

            if (!createResult.Succeeded)
            {
                throw new BusinessRuleException(
                    FormatIdentityErrors(createResult.Errors));
            }

            if (!await _roleManager.RoleExistsAsync(PatientRole))
            {
                var roleResult = await _roleManager.CreateAsync(
                    new IdentityRole(PatientRole));

                if (!roleResult.Succeeded)
                {
                    throw new BusinessRuleException(
                        FormatIdentityErrors(roleResult.Errors));
                }
            }

            var roleAssignmentResult =
                await _userManager.AddToRoleAsync(user, PatientRole);

            if (!roleAssignmentResult.Succeeded)
            {
                throw new BusinessRuleException(
                    FormatIdentityErrors(roleAssignmentResult.Errors));
            }

            var patient = new Patient
            {
                UserId = user.Id,
                Name = name,
                Email = email,
                Phone = phone,
                DateOfBirth = dateOfBirth,
                Allergies = allergies,
                ChronicConditions = chronicConditions
            };

            _unitOfWork.Patients.Add(patient);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var token = await GenerateTokenAsync(user);

            await transaction.CommitAsync(cancellationToken);

            return token;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<JwtTokenResult> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await _userManager.FindByEmailAsync(email);

        if (user is null ||
            !await _userManager.CheckPasswordAsync(user, password))
        {
            throw new UnauthorizedAccessException(
                "Invalid email or password.");
        }

        return await GenerateTokenAsync(user);
    }


    private async Task<JwtTokenResult> GenerateTokenAsync(
        ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);

        string? patientId = null;

        if (roles.Contains("Patient"))
        {
            var patients = await _unitOfWork.Patients.FindAsync(
                patient => patient.UserId == user.Id,
                cancellationToken: CancellationToken.None);

            patientId = patients
                .Select(patient => patient.Id)
                .FirstOrDefault()
                .ToString();

            if (patientId == "0")
            {
                throw new InvalidOperationException(
                    "The patient profile was not found.");
            }
        }

        return _jwtTokenService.GenerateToken(
            user.Id,
            user.Email ?? user.UserName ?? string.Empty,
            roles,
            patientId);
    }


    private static string FormatIdentityErrors(
        IEnumerable<IdentityError> errors)
    {
        return string.Join("; ",errors.Select(error => error.Description));
    }
}
