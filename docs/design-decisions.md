# Design decisions

- Daily rolling logs retain at most 14 files and default to `logs/hospitalcare-.log`; `Serilog:FilePath` can override the path.
- Department listing is patient-only, defaults to 20 items per page, caps page size at 100, and orders by department ID.
