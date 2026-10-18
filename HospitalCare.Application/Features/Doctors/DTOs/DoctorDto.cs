using System;
using System.Collections.Generic;
using System.Text;
using HospitalCare.Domain.Entities;

namespace HospitalCare.Application.Features.Doctors.DTOs
{
    public sealed record DoctorDto(
    int Id,
    string Name,
    int DepartmentId,
    bool IsActive,
    bool PendingDeactivation)
    {
        public static DoctorDto FromEntity(Doctor doctor)
        {
            return new DoctorDto(
                doctor.Id,
                doctor.Name,
                doctor.DepartmentId,
                doctor.IsActive,
                doctor.PendingDeactivation);
        }
    }
}