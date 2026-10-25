using HospitalCare.Application.Common.Constants;
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
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ApplicationDbContext _dbContext;

    public IdentityService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        RoleManager<IdentityRole> roleManager,
        IJwtTokenService jwtTokenService,
        IUnitOfWork unitOfWork,
        ApplicationDbContext dbContext)
    {
        _userManager = userManager;
        _signInManager = signInManager;
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

            if (!await _roleManager.RoleExistsAsync(Roles.Patient))
            {
                var roleResult = await _roleManager.CreateAsync(
                    new IdentityRole(Roles.Patient));

                if (!roleResult.Succeeded)
                {
                    throw new BusinessRuleException(
                        FormatIdentityErrors(roleResult.Errors));
                }
            }

            var roleAssignmentResult =
                await _userManager.AddToRoleAsync(user, Roles.Patient);

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
            !(await _signInManager.CheckPasswordSignInAsync(
                user,
                password,
                lockoutOnFailure: true)).Succeeded)
        {
            throw new UnauthorizedAccessException(
                "Invalid email or password.");
        }

        return await GenerateTokenAsync(user);
    }

    public async Task<Doctor> CreateDoctorAsync(
        string name,
        string email,
        string password,
        int departmentId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        try
        {
            if (await _userManager.FindByEmailAsync(email) is not null)
            {
                throw new ConflictException(
                    "An account with this email already exists.");
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email
            };

            var createResult = await _userManager.CreateAsync(user, password);

            if (!createResult.Succeeded)
            {
                throw new BusinessRuleException(
                    FormatIdentityErrors(createResult.Errors));
            }

            if (!await _roleManager.RoleExistsAsync(Roles.Doctor))
            {
                var roleResult = await _roleManager.CreateAsync(
                    new IdentityRole(Roles.Doctor));

                if (!roleResult.Succeeded)
                {
                    throw new BusinessRuleException(
                        FormatIdentityErrors(roleResult.Errors));
                }
            }

            var roleAssignmentResult =
                await _userManager.AddToRoleAsync(user, Roles.Doctor);

            if (!roleAssignmentResult.Succeeded)
            {
                throw new BusinessRuleException(
                    FormatIdentityErrors(roleAssignmentResult.Errors));
            }

            var doctor = new Doctor
            {
                UserId = user.Id,
                Name = name,
                DepartmentId = departmentId
            };

            _unitOfWork.Doctors.Add(doctor);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return doctor;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task CreateReceptionistAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        try
        {
            if (await _userManager.FindByEmailAsync(email) is not null)
            {
                throw new ConflictException(
                    "An account with this email already exists.");
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email
            };

            var createResult = await _userManager.CreateAsync(user, password);

            if (!createResult.Succeeded)
            {
                throw new BusinessRuleException(
                    FormatIdentityErrors(createResult.Errors));
            }

            if (!await _roleManager.RoleExistsAsync(Roles.Receptionist))
            {
                var roleResult = await _roleManager.CreateAsync(
                    new IdentityRole(Roles.Receptionist));

                if (!roleResult.Succeeded)
                {
                    throw new BusinessRuleException(
                        FormatIdentityErrors(roleResult.Errors));
                }
            }

            var roleAssignmentResult = await _userManager.AddToRoleAsync(
                user,
                Roles.Receptionist);

            if (!roleAssignmentResult.Succeeded)
            {
                throw new BusinessRuleException(
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


    private async Task<JwtTokenResult> GenerateTokenAsync(
        ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);

        string? patientId = null;
        string? doctorId = null;

        if (roles.Contains(Roles.Patient))
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

        if (roles.Contains(Roles.Doctor))
        {
            var doctors = await _unitOfWork.Doctors.FindAsync(
                doctor => doctor.UserId == user.Id,
                cancellationToken: CancellationToken.None);

            var id = doctors
                .Select(doctor => doctor.Id)
                .FirstOrDefault();

            if (id == 0)
            {
                throw new InvalidOperationException(
                    "The doctor profile was not found.");
            }

            doctorId = id.ToString();
        }

        return _jwtTokenService.GenerateToken(
            user.Id,
            user.Email ?? user.UserName ?? string.Empty,
            roles,
            patientId,
            doctorId);
    }


    private static string FormatIdentityErrors(
        IEnumerable<IdentityError> errors)
    {
        return string.Join("; ",errors.Select(error => error.Description));
    }
}
