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
