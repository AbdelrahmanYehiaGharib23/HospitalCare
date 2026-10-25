# Design decisions

- Daily rolling logs retain at most 14 files and default to `logs/hospitalcare-.log`; `Serilog:FilePath` can override the path.
