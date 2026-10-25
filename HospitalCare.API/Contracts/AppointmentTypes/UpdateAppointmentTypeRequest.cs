namespace HospitalCare.API.Contracts.AppointmentTypes;

public sealed record UpdateAppointmentTypeRequest(
    string Name,
    int DurationMinutes,
    decimal BookingFee);
