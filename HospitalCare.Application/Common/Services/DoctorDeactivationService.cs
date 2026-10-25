using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Domain.Enums;

namespace HospitalCare.Application.Common.Services;

public sealed class DoctorDeactivationService
{
    private readonly IUnitOfWork _unitOfWork;

    public DoctorDeactivationService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task ApplyPendingDeactivationAsync(
        int doctorId,
        CancellationToken cancellationToken)
    {
        var doctors = await _unitOfWork.Doctors.FindAsync(
            doctor => doctor.Id == doctorId,
            withTracking: true,
            cancellationToken);
        var doctor = doctors.SingleOrDefault();
        if (doctor is null)
        {
            return;
        }

        if (doctor.PendingDeactivation &&
            !await HasOpenAppointmentsAsync([doctorId], cancellationToken))
        {
            doctor.CompleteDeactivation();
        }

        var department = await _unitOfWork.Departments.GetByIdAsync(
            doctor.DepartmentId,
            cancellationToken);
        if (department is null || !department.PendingDeactivation)
        {
            return;
        }

        var departmentDoctors = (await _unitOfWork.Doctors.FindAsync(
            candidate => candidate.DepartmentId == department.Id,
            withTracking: true,
            cancellationToken)).ToArray();
        var departmentDoctorIds = departmentDoctors
            .Select(candidate => candidate.Id)
            .ToArray();
        if (await HasOpenAppointmentsAsync(
                departmentDoctorIds,
                cancellationToken))
        {
            return;
        }

        department.CompleteDeactivation();
        foreach (var departmentDoctor in departmentDoctors)
        {
            departmentDoctor.CompleteDeactivation();
        }
    }

    private async Task<bool> HasOpenAppointmentsAsync(
        IReadOnlyCollection<int> doctorIds,
        CancellationToken cancellationToken)
    {
        if (doctorIds.Count == 0)
        {
            return false;
        }

        var appointments = await _unitOfWork.Appointments.FindAsync(
            appointment =>
                doctorIds.Contains(appointment.DoctorId) &&
                (appointment.Status == AppointmentStatus.Queued ||
                    appointment.Status == AppointmentStatus.InProgress),
            withTracking: false,
            cancellationToken);

        return appointments.Any();
    }
}
