# Design decisions

- Daily rolling logs retain at most 14 files and default to `logs/hospitalcare-.log`; `Serilog:FilePath` can override the path.
- Department listing is patient-only, defaults to 20 items per page, caps page size at 100, and orders by department ID.
- Seeded appointment booking fees are sample values: 100 EGP for Consultation and 50 EGP for Follow-up.
- Existing appointment types receive 100 EGP during the fee migration, or 50 EGP when named Follow-up.
- Department updates change the name only; deactivation follows the graceful queue rule planned for M6.
- Doctor.UserId remains nullable for existing rows; newly created doctor profiles link to an Identity user.
- Patient doctor queries return active Doctors only; Admin can request inactive Doctors explicitly.
- Receptionist accounts are Identity users without a separate domain profile; the create response contains the email and role.
- Admin seeding requires both `SeedAdmin:Email` and `SeedAdmin:Password`; an existing non-Admin account is not promoted.
- DoctorSchedule replacement accepts an empty range set to clear the weekly schedule; patients can read schedules for active Doctors, Doctors can read their own, and Admins can read any.
- Available-date queries include both requested bounds; inactive Doctors return not found and pending-deactivation Doctors return no dates. Schedules use `Hospital:TimeZoneId`, default `Africa/Cairo`, while timestamps remain UTC.
- Available-date queries accept a maximum inclusive range of 90 calendar days.
- Doctors with `PendingDeactivation` reject new payment attempts while existing appointments drain.
- Queue positions are never reused; new appointments use max position plus one, including cancelled rows, while Patient responses show the current rank among `Queued` and `InProgress` appointments.
- The Fake payment provider uses an HMAC-SHA256 signature over the exact UTF-8 webhook payload and a configured secret; its checkout host is non-routable and refunds are local no-ops.
- Payment webhooks use a raw JSON event with `gatewayReference`, `status`, `amount`, and `currency`; `X-Payment-Signature` is hexadecimal HMAC-SHA256 over the exact UTF-8 body. This applies only to the internal Fake provider and defines no third-party signing format.
- Refund requests use the payment transaction ID as their idempotency key so an external refund can be retried after a transient failure.
- `RefundPending` records a confirmed payment awaiting external refund completion; database state commits before the gateway refund call so retries cannot lose the refund request.
