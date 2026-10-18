using System;
using System.Collections.Generic;
using System.Text;

namespace HospitalCare.Domain.Entities
{
    public class AppointmentType : BaseEntity
    {
        public string Name { get; set; } = string.Empty;

        public int DurationMinutes { get; set; }

        public ICollection<Appointment> Appointments { get; set; } =
            new List<Appointment>();

        public ICollection<PaymentTransaction> PaymentTransactions { get; set; } =
            new List<PaymentTransaction>();
    }
}
