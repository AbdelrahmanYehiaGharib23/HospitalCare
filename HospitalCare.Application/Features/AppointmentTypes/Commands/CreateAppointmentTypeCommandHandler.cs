using HospitalCare.Application.Common.Exceptions;
using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Application.Features.AppointmentTypes.DTOs;
using HospitalCare.Domain.Entities;
using MediatR;

namespace HospitalCare.Application.Features.AppointmentTypes.Commands;

public sealed class CreateAppointmentTypeCommandHandler
    : IRequestHandler<CreateAppointmentTypeCommand, AppointmentTypeDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public CreateAppointmentTypeCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<AppointmentTypeDto> Handle(
        CreateAppointmentTypeCommand request,
        CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        var existing = await _unitOfWork.AppointmentTypes.FindAsync(
            appointmentType =>
                appointmentType.Name.ToUpper() == name.ToUpper(),
            cancellationToken: cancellationToken);

        if (existing.Any())
        {
            throw new ConflictException(
                "An appointment type with this name already exists.");
        }

        var appointmentType = new AppointmentType
        {
            Name = name,
            DurationMinutes = request.DurationMinutes,
            BookingFee = request.BookingFee
        };

        _unitOfWork.AppointmentTypes.Add(appointmentType);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return AppointmentTypeDto.FromEntity(appointmentType);
    }
}
