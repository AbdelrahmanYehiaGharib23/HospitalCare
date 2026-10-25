using System;
using System.Collections.Generic;
namespace HospitalCare.Domain.Entities
{
    public class Doctor : BaseEntity
    {
        public string Name { get; set; } = string.Empty;

        public string? UserId { get; set; }

        public int DepartmentId { get; set; }

        public bool IsActive { get; set; } = true;

        public bool PendingDeactivation { get; set; }

        public Department Department { get; set; } = null!;

        public ICollection<Appointment> Appointments { get; set; } =
            new List<Appointment>();

        public ICollection<DoctorSchedule> Schedules { get; set; } =
            new List<DoctorSchedule>();

        public void RequestDeactivation(bool hasActiveAppointments)
        {
            if (!IsActive)
            {
                return;
            }

            if (hasActiveAppointments)
            {
                PendingDeactivation = true;
                return;
            }

            CompleteDeactivation();
        }

        public void CompleteDeactivation()
        {
            IsActive = false;
            PendingDeactivation = false;
        }
    }
}
