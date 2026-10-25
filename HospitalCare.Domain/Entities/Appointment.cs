using System;
using System.Collections.Generic;
using HospitalCare.Domain.Enums;

namespace HospitalCare.Domain.Entities
{
    public class Appointment : BaseEntity
    {
        public const int NoShowGracePeriodMinutes = 5;

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

        public DateTime? CalledAt { get; set; }

        public DateTime? CheckedInAt { get; set; }

        public int SkipCount { get; set; }

        public Doctor Doctor { get; set; } = null!;

        public Patient Patient { get; set; } = null!;

        public AppointmentType AppointmentType { get; set; } = null!;

        public PaymentTransaction? PaymentTransaction { get; set; }

        public void Cancel()
        {
            if (Status is not AppointmentStatus.Queued and
                not AppointmentStatus.InProgress)
            {
                throw new InvalidOperationException(
                    "Only queued or in-progress appointments can be cancelled.");
            }

            Status = AppointmentStatus.Cancelled;
        }

        public void CheckIn(DateTime checkedInAt)
        {
            EnsureQueued();
            EnsureUtc(checkedInAt);

            if (CheckedInAt.HasValue)
            {
                throw new InvalidOperationException(
                    "The Appointment has already been checked in.");
            }

            if (CalledAt.HasValue &&
                checkedInAt >= CalledAt.Value.AddMinutes(
                    NoShowGracePeriodMinutes))
            {
                CalledAt = null;
            }

            CheckedInAt = checkedInAt;
        }

        public void Call(DateTime calledAt)
        {
            EnsureQueued();
            EnsureUtc(calledAt);

            if (!CheckedInAt.HasValue)
            {
                throw new InvalidOperationException(
                    "Only checked-in Appointments can be called.");
            }

            if (CalledAt.HasValue)
            {
                throw new InvalidOperationException(
                    "The Appointment has already been called.");
            }

            CalledAt = calledAt;
        }

        public void Start(DateTime actualStartTime)
        {
            EnsureQueued();
            EnsureUtc(actualStartTime);

            if (!CheckedInAt.HasValue || !CalledAt.HasValue)
            {
                throw new InvalidOperationException(
                    "Only checked-in and called Appointments can start.");
            }

            if (ActualStartTime.HasValue)
            {
                throw new InvalidOperationException(
                    "The Appointment has already started.");
            }

            Status = AppointmentStatus.InProgress;
            ActualStartTime = actualStartTime;
        }

        public void Complete(DateTime actualEndTime, string? notes)
        {
            if (Status != AppointmentStatus.InProgress)
            {
                throw new InvalidOperationException(
                    "Only in-progress Appointments can be completed.");
            }

            if (ActualEndTime.HasValue)
            {
                throw new InvalidOperationException(
                    "The Appointment has already completed.");
            }

            EnsureUtc(actualEndTime);
            if (ActualStartTime.HasValue &&
                actualEndTime < ActualStartTime.Value)
            {
                throw new InvalidOperationException(
                    "The completion time cannot be before the start time.");
            }

            Status = AppointmentStatus.Completed;
            ActualEndTime = actualEndTime;
            Notes = notes;
        }

        public void MoveToQueuePosition(int queuePosition)
        {
            EnsureQueued();
            if (queuePosition <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(queuePosition));
            }

            QueuePosition = queuePosition;
        }

        public void RecordFirstNoShow(
            int nextQueuePosition,
            DateTime utcNow)
        {
            EnsureMissedCall();
            if (!IsNoShowDue(utcNow))
            {
                throw new InvalidOperationException(
                    "The call window has not expired.");
            }

            if (SkipCount != 0)
            {
                throw new InvalidOperationException(
                    "The Appointment has already been skipped.");
            }

            MoveToQueuePosition(nextQueuePosition);
            SkipCount = 1;
            CalledAt = null;
        }

        public void PenalizeNoShow(DateTime utcNow)
        {
            EnsureMissedCall();
            if (!IsNoShowDue(utcNow))
            {
                throw new InvalidOperationException(
                    "The call window has not expired.");
            }

            if (SkipCount == 0)
            {
                throw new InvalidOperationException(
                    "The first missed call must be swapped before a penalty.");
            }

            Status = AppointmentStatus.NoShowPenalized;
            CalledAt = null;
        }

        public bool IsNoShowDue(DateTime utcNow)
        {
            EnsureUtc(utcNow);
            return Status == AppointmentStatus.Queued &&
                !CheckedInAt.HasValue &&
                CalledAt.HasValue &&
                CalledAt.Value.AddMinutes(NoShowGracePeriodMinutes) <= utcNow;
        }

        private void EnsureQueued()
        {
            if (Status != AppointmentStatus.Queued)
            {
                throw new InvalidOperationException(
                    "Only queued Appointments can use this operation.");
            }
        }

        private void EnsureMissedCall()
        {
            EnsureQueued();
            if (CheckedInAt.HasValue || !CalledAt.HasValue)
            {
                throw new InvalidOperationException(
                    "The Appointment has not missed a call.");
            }
        }

        private static void EnsureUtc(DateTime value)
        {
            if (value.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Appointment timestamps must be UTC.",
                    nameof(value));
            }
        }
    }
}
