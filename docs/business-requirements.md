# HospitalCare — Business Requirements Document

## 1. Overview

HospitalCare is a medium-sized, multi-department hospital appointment scheduling system. The technical foundation (Clean Architecture across Domain / Application / Infrastructure / API, CQRS with MediatR, Generic Repository + Unit of Work, DTO mapping) is already implemented. This document defines the business layer that foundation is built to serve: entities, business rules, use cases, validation rules, role permissions, and the CQRS commands/queries needed to implement them.

## 2. Actors

| Actor | Role in the system |
|---|---|
| **Admin** | Manages departments, doctors, appointment types, and receptionist accounts |
| **Doctor** | Manages their own schedule; runs their daily queue (start/complete appointments) |
| **Receptionist** | Oversight across all doctors' queues; can act on a patient's behalf |
| **Patient** | Self-registers; books, views, and cancels their own appointments |

## 3. Domain Entities & Relationships

### Already implemented
```
Department (1) ── (*) Doctor
Doctor     (1) ── (*) Appointment
Patient    (1) ── (*) Appointment
Appointment ── DoctorId, PatientId
```

### New entities required by the rules below
```
Doctor          (1) ── (*) DoctorSchedule
AppointmentType (1) ── (*) Appointment
Appointment     (1) ── (1) PaymentTransaction
```

**Department**
- `Id`, `Name`, `IsActive`

**Doctor** *(extended)*
- `Id`, `UserId`, `Name`, `DepartmentId`, `IsActive`, `PendingDeactivation`

**DoctorSchedule** *(new)*
- `Id`, `DoctorId`, `DayOfWeek`, `StartTime`, `EndTime`

**Patient** *(extended)*
- `Id`, `Name`, `Email`, `Phone`, `DateOfBirth`, `Allergies`, `ChronicConditions`

**AppointmentType** *(new)*
- `Id`, `Name` (e.g. "Consultation", "Follow-up"), `DurationMinutes`, `BookingFee` (`decimal(18,2)`, EGP)

**PaymentTransaction** *(new)*
- `Id`, `PatientId`, `DoctorId`, `AppointmentTypeId`, `RequestedDate`, `Amount`, `Status` (`Pending` / `Succeeded` / `Failed` / `RefundPending` / `Refunded`), `GatewayReference`, `CreatedAt`

**Appointment** *(extended)*
- `Id`, `DoctorId`, `PatientId`, `AppointmentTypeId`, `PaymentTransactionId`
- `AppointmentDate` (date only — queue-based, not a fixed clock time)
- `QueuePosition`
- `Status` (`Queued` / `InProgress` / `Completed` / `Cancelled` / `NoShowPenalized`)
- `ActualStartTime`, `ActualEndTime` (nullable)
- `Notes`

## 4. Business Rules

### Department
- **BR-01** — Deactivating a Department cascades to all its Doctors; each Doctor follows the same graceful deactivation rule (BR-05).

### Doctor & Scheduling
- **BR-02** — Each Doctor defines their own working schedule (`DoctorSchedule`) — working days and hours differ per doctor; there is no universal fixed schedule.
- **BR-03** — Daily capacity is a time budget, not a patient count. A booking of `AppointmentType` T for date X is accepted only if `PendingMinutes + T.DurationMinutes ≤ AvailableMinutes`, where:
  - `AvailableMinutes` = total length of the Doctor's `DoctorSchedule` ranges for X's day of the week (summed when there are several ranges, 0 when there are none). For today, only the part of each range after the current time counts; for a past date it is 0.
  - `PendingMinutes` = sum of `DurationMinutes` of the Doctor's Appointments on X with Status `Queued` or `InProgress`. `Completed` appointments are not counted (that time has already passed), and `Cancelled` / `NoShowPenalized` ones are never counted (their time is released or was used by another patient).
  - Example: a 09:00–17:00 day is 480 minutes. With 4 Consultations (30 min) and 2 Follow-ups (15 min) pending (150 minutes), a new 30-minute Consultation is accepted, since 150 + 30 ≤ 480. The number of appointments per day therefore depends on the mix of types. Capacity is never manually set and never unlimited.
- **BR-04** — Appointment duration is determined by `AppointmentType` (e.g. Consultation vs. Follow-up have different fixed durations), not chosen freely per booking.
- **BR-05** — If a Doctor with future Appointments is deactivated, they remain active/visible until those Appointments complete, then the system auto-deactivates them. No Appointment is force-cancelled and no refund is triggered by this action alone.

### Booking & Queue
- **BR-06** — Booking is queue-based: a Patient selects a Doctor, a date, and an `AppointmentType`, and receives a queue position for that date — not a fixed clock-time slot.
- **BR-07** — A booking is registered in the queue automatically once capacity validation (BR-03) passes — no manual Receptionist or Doctor approval step exists.
- **BR-08** — The Doctor records `ActualStartTime`/`ActualEndTime` per Appointment via Start/Complete actions. The gap between actual and scheduled progress is used to compute a live estimated delay for patients later in the queue.

### Payment
- **BR-09** — Booking requires an online booking fee/deposit (not the full consultation cost), collected through a real payment gateway (e.g. Paymob) at booking time.
- **BR-10** — The queue position is assigned only after the gateway confirms successful payment. No slot is held or reserved while payment is in progress.
- **BR-11** — If payment succeeds but the Doctor's daily capacity has since filled (a capacity race), the booking is rejected and the payment is automatically refunded.
- **BR-12** — If a Patient cancels a confirmed booking, they receive a full refund of the booking fee, regardless of timing.

### No-show Handling
- **BR-13** — If a Patient is not present within 5 minutes of their queue turn, the next Patient is served instead; the late Patient swaps into that Patient's position (moves back exactly one place) and remains in the queue. No fee is forfeited on this first occurrence.
- **BR-14** — If the same Patient is skipped a second consecutive time, their booking fee is forfeited, since another Patient has already been treated in their place.

### Patient Record
- **BR-15** — A Patient's record includes basic medical history (allergies, chronic conditions) visible to the Doctor during consultation, alongside standard scheduling/contact details.

## 5. Use Cases

**Patient**
- UC-01 Register Patient Account
- UC-02 Browse Departments & Doctors
- UC-03 View a Doctor's Schedule & Available Dates
- UC-04 Book an Appointment (select Doctor + date + AppointmentType → pay → get queued)
- UC-05 View My Appointments & Queue Position
- UC-06 Cancel an Appointment
- UC-07 View/Update My Medical History

**Doctor**
- UC-08 View Today's Queue
- UC-09 Start an Appointment
- UC-10 Complete an Appointment (add consultation notes)
- UC-11 Manage My Schedule

**Receptionist**
- UC-12 View Any Doctor's Queue
- UC-13 Cancel an Appointment on a Patient's Behalf

**Admin**
- UC-14 Manage Departments (create, deactivate)
- UC-15 Manage Doctors (create, assign to Department, deactivate)
- UC-16 Manage Appointment Types (create, set duration)

**System (automated)**
- UC-17 Handle Payment Gateway Callback (confirm payment → validate capacity → create Appointment or refund)
- UC-18 Process No-show Swap (~5 minutes after a missed turn)
- UC-19 Auto-deactivate Doctor/Department once pending appointments complete

## 6. Validation Rules

| # | Attached to | Rule |
|---|---|---|
| VR-01 | Book Appointment | Requested date must fall on a day in the Doctor's `DoctorSchedule`, and the Doctor must be active |
| VR-02 | Book Appointment | The requested `AppointmentType` must fit in the Doctor's remaining time for that date (BR-03: `PendingMinutes + T.DurationMinutes ≤ AvailableMinutes`). Checked at `InitiateAppointmentPaymentCommand` for early rejection, and again at `ConfirmAppointmentPaymentCommand` as the authoritative check |
| VR-03 | Book Appointment | An `Appointment` is created only after `PaymentTransaction.Status == Succeeded` |
| VR-04 | Cancel Appointment | Only the owning Patient, a Receptionist, or an Admin may cancel; only `Queued`/`InProgress` appointments are cancellable |
| VR-05 | Start Appointment | Only the assigned Doctor may start it; only `Queued` appointments may transition to `InProgress` |
| VR-06 | Complete Appointment | Only the assigned Doctor may complete it; only `InProgress` appointments may transition to `Completed` |
| VR-07 | Deactivate Doctor | If the Doctor has `Queued`/`InProgress` appointments, deactivation is deferred (`PendingDeactivation = true`) instead of applied immediately |
| VR-08 | Deactivate Department | Deactivation cascades VR-07 to every Doctor in the Department |
| VR-09 | View Available Dates | The inclusive requested date range cannot exceed 90 calendar days |

## 7. Allowed / Forbidden Operations

| Operation | Admin | Doctor | Receptionist | Patient |
|---|---|---|---|---|
| Register own Patient profile | No | No | No | Yes |
| View medical history | No | Yes (own patients) | Yes | Yes (own) |
| Book Appointment | No | No | No | Yes (own) |
| Cancel Appointment | Yes (any) | No | Yes (any) | Yes (own only) |
| Start / Complete Appointment | No | Yes (own queue) | No | No |
| Check in Patient | No | No | Yes | No |
| Call next Patient | No | Yes (own queue) | Yes (any queue) | No |
| View Doctor Queue | No | Yes (own) | Yes (all) | No |
| View DoctorSchedule | Yes (any) | Yes (own) | No | Yes (active Doctors) |
| Manage own DoctorSchedule | No | Yes | No | No |
| Manage Departments | Yes | No | No | No |
| Manage Doctors | Yes | No | No | No |
| Manage Appointment Types | Yes | No | No | No |
| Create Receptionist Account | Yes | No | No | No |

## 8. CQRS Commands & Queries

**Commands**
- `RegisterPatientCommand`
- `InitiateAppointmentPaymentCommand` — pre-validates schedule/capacity, creates a `PaymentTransaction`, returns the gateway session
- `ConfirmAppointmentPaymentCommand` — invoked by the payment gateway webhook; re-validates capacity, then creates the `Appointment` + `QueuePosition`, or triggers a refund
- `CancelAppointmentCommand`
- `StartAppointmentCommand`
- `CompleteAppointmentCommand`
- `ProcessNoShowSwapCommand` — system-triggered
- `CreateDepartmentCommand` / `DeactivateDepartmentCommand`
- `CreateDoctorCommand` / `UpdateDoctorScheduleCommand` / `DeactivateDoctorCommand`
- `CreateAppointmentTypeCommand`
- `CreateReceptionistAccountCommand`

**Queries**
- `GetDepartmentsQuery` *(already implemented)*
- `GetDoctorsByDepartmentQuery`
- `GetDoctorByIdQuery`
- `GetDoctorScheduleQuery`
- `GetDoctorAvailableDatesQuery` — takes an `AppointmentTypeId`, because a date may still fit a Follow-up but not a Consultation (BR-03)
- `GetPatientAppointmentsQuery`
- `GetDoctorQueueForDateQuery` — includes computed estimated delay per queued patient
- `GetAppointmentByIdQuery`

## 9. Open Items (implementation-level, not blocking)

- Exact booking fee amount per `AppointmentType`
- Initial seed list of `AppointmentType` values and their `DurationMinutes`
- Whether the 5-minute no-show grace period is configurable per doctor or fixed system-wide
- Receptionist's exact write-access boundaries beyond what's listed in section 7
- Optional safety margin: whether to cap bookings below 100% of working time (e.g. 90%) to absorb overruns

## 10. Decisions Applied

- A Doctor with `PendingDeactivation` accepts no new bookings while remaining active until the queue drains.
- `BookingFee` is stored per `AppointmentType` in EGP.
- `QueuePosition` is assigned as the next value after the highest position for that Doctor and date, including cancelled appointments; a Patient sees their rank among `Queued` and `InProgress` appointments.
- The configured payment provider is selected through `IPaymentGateway`; only the internal Fake provider is implemented until an external provider is explicitly approved.
- Available-date queries accept an inclusive range of at most 90 calendar days.
- A Doctor with `PendingDeactivation` accepts no new bookings, allowing the existing queue to drain.
- `QueuePosition` is the next value after the highest value for the Doctor and date, including cancelled appointments; a Patient sees their rank among `Queued` and `InProgress` appointments.
- `RefundPending` records a confirmed payment that requires a refund; the payment transaction ID is the gateway refund idempotency key.
- A Department stays active with `PendingDeactivation` while any Doctor has queued or in-progress Appointments; each Doctor stops accepting bookings while its own queue drains.
- A Patient is present only after Receptionist check-in. A Doctor or Receptionist calls the next checked-in Patient, starting the five-minute no-show window.
- If the first missed call has no later queued Patient to swap with, it remains pending and `SkipCount` is unchanged until a next Patient exists.
- The five-minute no-show window is fixed system-wide. Queue call actions apply to the hospital-local current date.
- No-show swaps exchange positions between existing queue entries; new bookings still use max position plus one, including cancelled rows.
