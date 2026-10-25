using System;
using System.Collections.Generic;
namespace HospitalCare.Domain.Entities
{

    public class Department : BaseEntity
    {
        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public bool PendingDeactivation { get; set; }

        public ICollection<Doctor> Doctors { get; set; } = new List<Doctor>();

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
