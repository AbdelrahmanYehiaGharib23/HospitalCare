# Design decisions

- Daily rolling logs retain at most 14 files and default to `logs/hospitalcare-.log`; `Serilog:FilePath` can override the path.
- Department listing is patient-only, defaults to 20 items per page, caps page size at 100, and orders by department ID.
- Seeded appointment booking fees are sample values: 100 EGP for Consultation and 50 EGP for Follow-up.
- Existing appointment types receive 100 EGP during the fee migration, or 50 EGP when named Follow-up.
