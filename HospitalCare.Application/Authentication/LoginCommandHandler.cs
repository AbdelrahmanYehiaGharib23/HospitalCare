using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Application.Common.Models;
using MediatR;

namespace HospitalCare.Application.Authentication;

public sealed class LoginCommandHandler
    : IRequestHandler<LoginCommand, JwtTokenResult>
{
    private readonly IIdentityService _identityService;

    public LoginCommandHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public Task<JwtTokenResult> Handle(
        LoginCommand request,
        CancellationToken cancellationToken)
    {
        return _identityService.LoginAsync(
            request.Email,
            request.Password,
            cancellationToken);
    }
}
