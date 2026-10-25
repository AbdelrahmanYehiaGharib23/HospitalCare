using HospitalCare.Application.Features.Doctors.DTOs;
using MediatR;

namespace HospitalCare.Application.Features.Doctors.Commands;

public sealed record DeactivateDoctorCommand(int DoctorId)
    : IRequest<DoctorDto>;
