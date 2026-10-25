using HospitalCare.Application.Features.Receptionists.DTOs;
using MediatR;

namespace HospitalCare.Application.Features.Receptionists.Commands;

public sealed record CreateReceptionistAccountCommand(
    string Email,
    string Password) : IRequest<ReceptionistAccountDto>;
