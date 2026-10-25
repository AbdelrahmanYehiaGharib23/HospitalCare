using HospitalCare.Application.Common.Exceptions;
using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Application.Features.AppointmentTypes.DTOs;
using MediatR;

namespace HospitalCare.Application.Features.AppointmentTypes.Commands;

public sealed class UpdateAppointmentTypeCommandHandler
    : IRequestHandler<UpdateAppointmentTypeCommand, AppointmentTypeDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public UpdateAppointmentTypeCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<AppointmentTypeDto> Handle(
        UpdateAppointmentTypeCommand request,
        CancellationToken cancellationToken)
    {
        var appointmentType = await _unitOfWork.AppointmentTypes.GetByIdAsync(
            request.Id,
            cancellationToken);

        if (appointmentType is null)
        {
            throw new NotFoundException("Appointment type was not found.");
        }

        var name = request.Name.Trim();
        var existing = await _unitOfWork.AppointmentTypes.FindAsync(
            candidate =>
                candidate.Id != request.Id &&
                candidate.Name.ToUpper() == name.ToUpper(),
            cancellationToken: cancellationToken);

        if (existing.Any())
        {
            throw new ConflictException(
                "An appointment type with this name already exists.");
        }

        appointmentType.Name = name;
        appointmentType.DurationMinutes = request.DurationMinutes;
        appointmentType.BookingFee = request.BookingFee;

        _unitOfWork.AppointmentTypes.Update(appointmentType);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return AppointmentTypeDto.FromEntity(appointmentType);
    }
}
