using System;
using System.Collections.Generic;
using System.Text;
using HospitalCare.Domain.Enums;

namespace HospitalCare.Domain.Entities
{
    public class Appointment : BaseEntity
    {
        public int DoctorId { get; set; }

        public int PatientId { get; set; }

        public int AppointmentTypeId { get; set; }

        public int? PaymentTransactionId { get; set; }

        public DateOnly AppointmentDate { get; set; }

        public int QueuePosition { get; set; }

        public AppointmentStatus Status { get; set; } =
            AppointmentStatus.Queued;

        public DateTime? ActualStartTime { get; set; }

        public DateTime? ActualEndTime { get; set; }

        public string? Notes { get; set; }

        public Doctor Doctor { get; set; } = null!;

        public Patient Patient { get; set; } = null!;

        public AppointmentType AppointmentType { get; set; } = null!;

        public PaymentTransaction? PaymentTransaction { get; set; }
    }
}
