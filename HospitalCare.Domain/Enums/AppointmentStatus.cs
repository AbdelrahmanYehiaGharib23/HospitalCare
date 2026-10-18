using System;
using System.Collections.Generic;
using System.Text;

namespace HospitalCare.Domain.Enums
{
    public enum AppointmentStatus
    {
        Queued = 1,
        InProgress = 2,
        Completed = 3,
        Cancelled = 4,
        NoShowPenalized = 5
    }
}
