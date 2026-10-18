using System;
using System.Collections.Generic;
using System.Text;

namespace HospitalCare.Domain.Entities
{
    public class Doctor : BaseEntity
    {
        public string Name { get; set; } = string.Empty;

        public int DepartmentId { get; set; }

        public bool IsActive { get; set; } = true;

        public bool PendingDeactivation { get; set; }

        public Department Department { get; set; } = null!;

        public ICollection<Appointment> Appointments { get; set; } =
            new List<Appointment>();

        public ICollection<DoctorSchedule> Schedules { get; set; } =
            new List<DoctorSchedule>();
    }
}
