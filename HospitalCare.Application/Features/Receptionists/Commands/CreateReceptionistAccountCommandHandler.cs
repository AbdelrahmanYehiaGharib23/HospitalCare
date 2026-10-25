using HospitalCare.Application.Common.Constants;
using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Application.Features.Receptionists.DTOs;
using MediatR;

namespace HospitalCare.Application.Features.Receptionists.Commands;

public sealed class CreateReceptionistAccountCommandHandler
    : IRequestHandler<CreateReceptionistAccountCommand, ReceptionistAccountDto>
{
    private readonly IIdentityService _identityService;

    public CreateReceptionistAccountCommandHandler(
        IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task<ReceptionistAccountDto> Handle(
        CreateReceptionistAccountCommand request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();

        await _identityService.CreateReceptionistAsync(
            email,
            request.Password,
            cancellationToken);

        return new ReceptionistAccountDto(email, Roles.Receptionist);
    }
}
