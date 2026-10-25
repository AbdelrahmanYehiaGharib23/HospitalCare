using HospitalCare.Application.Features.Doctors.DTOs;
using MediatR;

namespace HospitalCare.Application.Features.Doctors.Commands;

public sealed record CreateDoctorCommand(
    string Name,
    string Email,
    string Password,
    int DepartmentId) : IRequest<DoctorDto>;
