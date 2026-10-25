using HospitalCare.Application.Features.Doctors.DTOs;
using MediatR;

namespace HospitalCare.Application.Features.Doctors.Queries;

public sealed record GetDoctorByIdQuery(int Id) : IRequest<DoctorDto>;
