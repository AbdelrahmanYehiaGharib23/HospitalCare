using System;
using System.Collections.Generic;
using System.Text;

namespace HospitalCare.Domain.Entities
{
    public class DoctorSchedule : BaseEntity
    {
        public int DoctorId { get; set; }

        public DayOfWeek DayOfWeek { get; set; }

        public TimeOnly StartTime { get; set; }

        public TimeOnly EndTime { get; set; }

        public Doctor Doctor { get; set; } = null!;
    }
}
