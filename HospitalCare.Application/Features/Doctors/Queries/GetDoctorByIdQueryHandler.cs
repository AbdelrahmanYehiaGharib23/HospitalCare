using HospitalCare.Application.Common.Constants;
using HospitalCare.Application.Common.Exceptions;
using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Application.Features.Doctors.DTOs;
using MediatR;

namespace HospitalCare.Application.Features.Doctors.Queries;

public sealed class GetDoctorByIdQueryHandler
    : IRequestHandler<GetDoctorByIdQuery, DoctorDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public GetDoctorByIdQueryHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<DoctorDto> Handle(
        GetDoctorByIdQuery request,
        CancellationToken cancellationToken)
    {
        var doctors = await _unitOfWork.Doctors.FindAsync(
            doctor => doctor.Id == request.Id,
            withTracking: false,
            cancellationToken);
        var doctor = doctors.SingleOrDefault();

        if (doctor is null ||
            (!doctor.IsActive &&
                !_currentUserService.IsInRole(Roles.Admin)))
        {
            throw new NotFoundException("Doctor was not found.");
        }

        return DoctorDto.FromEntity(doctor);
    }
}
