using HospitalCare.Application.Common.Exceptions;
using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Application.Common.Models;
using HospitalCare.Application.Features.Payments.DTOs;
using HospitalCare.Domain.Entities;
using HospitalCare.Domain.Enums;
using HospitalCare.Domain.Services;
using MediatR;

namespace HospitalCare.Application.Features.Appointments.Commands;

public sealed class InitiateAppointmentPaymentCommandHandler
    : IRequestHandler<
        InitiateAppointmentPaymentCommand,
        InitiatedAppointmentPaymentDto>
{
    private const string Currency = "EGP";

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPaymentGateway _paymentGateway;
    private readonly DoctorCapacityCalculator _capacityCalculator;
    private readonly TimeProvider _timeProvider;
    private readonly TimeZoneInfo _hospitalTimeZone;

    public InitiateAppointmentPaymentCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IPaymentGateway paymentGateway,
        DoctorCapacityCalculator capacityCalculator,
        TimeProvider timeProvider,
        TimeZoneInfo hospitalTimeZone)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _paymentGateway = paymentGateway;
        _capacityCalculator = capacityCalculator;
        _timeProvider = timeProvider;
        _hospitalTimeZone = hospitalTimeZone;
    }

    public async Task<InitiatedAppointmentPaymentDto> Handle(
        InitiateAppointmentPaymentCommand request,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(_currentUserService.PatientId, out var patientId))
        {
            throw new ForbiddenException(
                "The authenticated Patient profile was not found.");
        }

        var patients = await _unitOfWork.Patients.FindAsync(
            patient => patient.Id == patientId,
            withTracking: false,
            cancellationToken);
        var patient = patients.SingleOrDefault()
            ?? throw new NotFoundException("Patient was not found.");

        var doctors = await _unitOfWork.Doctors.FindAsync(
            doctor => doctor.Id == request.DoctorId,
            withTracking: false,
            cancellationToken);
        var doctor = doctors.SingleOrDefault();
        if (doctor is null || !doctor.IsActive)
        {
            throw new NotFoundException("Doctor was not found.");
        }

        if (doctor.PendingDeactivation)
        {
            throw new ConflictException(
                "The Doctor is not accepting new bookings.");
        }

        var departments = await _unitOfWork.Departments.FindAsync(
            department => department.Id == doctor.DepartmentId,
            withTracking: false,
            cancellationToken);
        var department = departments.SingleOrDefault();
        if (department is null || !department.IsActive)
        {
            throw new NotFoundException("Department was not found.");
        }

        if (department.PendingDeactivation)
        {
            throw new ConflictException(
                "The Department is not accepting new bookings.");
        }

        var appointmentTypes = await _unitOfWork.AppointmentTypes.FindAsync(
            appointmentType =>
                appointmentType.Id == request.AppointmentTypeId,
            withTracking: false,
            cancellationToken);
        var appointmentType = appointmentTypes.SingleOrDefault();
        if (appointmentType is null)
        {
            throw new NotFoundException("Appointment type was not found.");
        }

        await EnsureCapacityAsync(
            request.DoctorId,
            request.RequestedDate,
            appointmentType.DurationMinutes,
            cancellationToken);

        var paymentTransaction = new PaymentTransaction
        {
            PatientId = patientId,
            DoctorId = request.DoctorId,
            AppointmentTypeId = request.AppointmentTypeId,
            RequestedDate = request.RequestedDate,
            Amount = appointmentType.BookingFee,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        };

        _unitOfWork.PaymentTransactions.Add(paymentTransaction);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var merchantReference = $"hospitalcare-payment-{paymentTransaction.Id}";

        var checkout = await _paymentGateway.CreateCheckoutAsync(
            new PaymentCheckoutRequest(
                paymentTransaction.Id,
                paymentTransaction.Amount,
                Currency,
                merchantReference,
                patient.Name,
                patient.Email,
                patient.Phone),
            cancellationToken);

        paymentTransaction.GatewayReference = checkout.GatewayReference;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new InitiatedAppointmentPaymentDto(
            paymentTransaction.Id,
            paymentTransaction.Amount,
            Currency,
            checkout.CheckoutUrl);
    }

    private async Task EnsureCapacityAsync(
        int doctorId,
        DateOnly requestedDate,
        int requestedDurationMinutes,
        CancellationToken cancellationToken)
    {
        var schedules = (await _unitOfWork.DoctorSchedules.FindAsync(
            schedule =>
                schedule.DoctorId == doctorId &&
                schedule.DayOfWeek == requestedDate.DayOfWeek,
            withTracking: false,
            cancellationToken)).ToArray();
        if (schedules.Length == 0)
        {
            throw new BusinessRuleException(
                "The Doctor is not scheduled on the requested date.");
        }

        var appointments = (await _unitOfWork.Appointments.FindAsync(
            appointment =>
                appointment.DoctorId == doctorId &&
                appointment.AppointmentDate == requestedDate &&
                (appointment.Status == AppointmentStatus.Queued ||
                    appointment.Status == AppointmentStatus.InProgress),
            withTracking: false,
            cancellationToken)).ToArray();
        var existingAppointmentTypeIds = appointments
            .Select(appointment => appointment.AppointmentTypeId)
            .Distinct()
            .ToArray();
        var durations = existingAppointmentTypeIds.Length == 0
            ? new Dictionary<int, int>()
            : (await _unitOfWork.AppointmentTypes.FindAsync(
                    type => existingAppointmentTypeIds.Contains(type.Id),
                    withTracking: false,
                    cancellationToken))
                .ToDictionary(type => type.Id, type => type.DurationMinutes);
        var capacityAppointments = appointments
            .Select(appointment => new DoctorCapacityAppointment(
                appointment.AppointmentDate,
                appointment.Status,
                durations[appointment.AppointmentTypeId]))
            .ToArray();
        var hospitalLocalNow = TimeZoneInfo.ConvertTime(
            _timeProvider.GetUtcNow(),
            _hospitalTimeZone);
        var capacity = _capacityCalculator.Calculate(
            requestedDate,
            schedules,
            capacityAppointments,
            requestedDurationMinutes,
            hospitalLocalNow);
        if (!capacity.CanFit)
        {
            throw new BusinessRuleException(
                "The appointment type does not fit in the Doctor's remaining capacity.");
        }
    }
}
