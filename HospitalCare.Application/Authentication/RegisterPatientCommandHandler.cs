using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Application.Common.Models;
using MediatR;

namespace HospitalCare.Application.Authentication;

public sealed class RegisterPatientCommandHandler: IRequestHandler<RegisterPatientCommand, JwtTokenResult>
{
    private readonly IIdentityService _identityService;

    public RegisterPatientCommandHandler(
        IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public Task<JwtTokenResult> Handle(
        RegisterPatientCommand request,
        CancellationToken cancellationToken)
    {
        return _identityService.RegisterAsync(
            request.Name,
            request.Email,
            request.Password,
            request.Phone,
            request.DateOfBirth,
            request.Allergies,
            request.ChronicConditions,
            cancellationToken);
    }
}
