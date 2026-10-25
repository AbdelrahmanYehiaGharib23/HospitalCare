using HospitalCare.Domain.Entities;
using HospitalCare.Domain.Enums;

namespace HospitalCare.Application.Tests.Appointments;

public sealed class AppointmentTransitionTests
{
    [Theory]
    [InlineData(AppointmentStatus.Queued)]
    [InlineData(AppointmentStatus.InProgress)]
    public void Cancel_ActiveAppointment_SetsCancelled(
        AppointmentStatus status)
    {
        var appointment = new Appointment { Status = status };

        appointment.Cancel();

        Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
    }

    [Theory]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.NoShowPenalized)]
    public void Cancel_TerminalAppointment_Throws(
        AppointmentStatus status)
    {
        var appointment = new Appointment { Status = status };

        var exception = Assert.Throws<InvalidOperationException>(
            appointment.Cancel);

        Assert.Contains("Only queued or in-progress", exception.Message);
    }
}
