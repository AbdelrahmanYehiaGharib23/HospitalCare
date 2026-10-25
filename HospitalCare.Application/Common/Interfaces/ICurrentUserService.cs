namespace HospitalCare.Application.Common.Interfaces;

public interface ICurrentUserService
{
    string? UserId { get; }

    string? PatientId { get; }

    string? DoctorId { get; }

    string? Email { get; }

    bool IsAuthenticated { get; }

    bool IsInRole(string role);
}
