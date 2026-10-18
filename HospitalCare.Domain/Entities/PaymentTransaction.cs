using System;
using System.Collections.Generic;
using System.Text;
using HospitalCare.Domain.Enums;

namespace HospitalCare.Domain.Entities
{
    public class PaymentTransaction : BaseEntity
    {
        public int PatientId { get; set; }

        public int DoctorId { get; set; }

        public int AppointmentTypeId { get; set; }

        public DateOnly RequestedDate { get; set; }

        public decimal Amount { get; set; }

        public PaymentStatus Status { get; set; } =
            PaymentStatus.Pending;

        public string? GatewayReference { get; set; }

        public DateTime CreatedAt { get; set; }

        public Patient Patient { get; set; } = null!;

        public Doctor Doctor { get; set; } = null!;

        public AppointmentType AppointmentType { get; set; } = null!;

        public Appointment? Appointment { get; set; }
    }
}
