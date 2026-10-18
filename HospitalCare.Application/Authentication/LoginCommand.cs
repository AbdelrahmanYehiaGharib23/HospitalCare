using HospitalCare.Application.Common.Models;
using MediatR;

namespace HospitalCare.Application.Authentication;

public sealed record LoginCommand(string Email,string Password) : IRequest<JwtTokenResult>;
