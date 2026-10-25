namespace HospitalCare.Domain.Services;

public sealed record DoctorCapacityResult(
    int AvailableMinutes,
    int PendingMinutes,
    bool CanFit);
