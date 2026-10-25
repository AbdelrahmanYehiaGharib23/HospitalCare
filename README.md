# HospitalCare

HospitalCare is a REST API for hospital appointment management.

## Stack

- .NET 10 and ASP.NET Core
- Entity Framework Core and SQL Server
- ASP.NET Core Identity and JWT bearer authentication
- MediatR for CQRS and FluentValidation for request validation

## Project layout

- `HospitalCare.Domain` contains the hospital entities and domain types.
- `HospitalCare.Application` contains use cases, validation, DTOs, and interfaces used by the application layer.
- `HospitalCare.Infrastructure` implements persistence, identity, and external service integrations.
- `HospitalCare.API` hosts the HTTP API and wires the application and infrastructure layers together.

## Requirements

- .NET 10 SDK
- SQL Server

## Configuration

The API reads the connection string from `ConnectionStrings:DefaultConnection`. The checked-in `appsettings.json` uses a local SQL Server instance named `.` and Windows integrated authentication. Set a user secret to use a different connection string:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "YOUR_SQL_SERVER_CONNECTION_STRING" --project HospitalCare.API
```

The API also requires `Jwt:SecretKey`. Store a unique secret with at least 32 UTF-8 bytes in user secrets:

```powershell
dotnet user-secrets set "Jwt:SecretKey" "REPLACE_WITH_A_RANDOM_SECRET_OF_AT_LEAST_32_UTF8_BYTES" --project HospitalCare.API
```

Replace the example value before running the command. `Jwt:Issuer` and `Jwt:Audience` have development values in `HospitalCare.API/appsettings.json`. User secrets are stored outside the repository. Environment variables can also be used with the names `ConnectionStrings__DefaultConnection` and `Jwt__SecretKey`.

## Database

On startup, `DbInitializer` applies pending Entity Framework migrations and seeds the `Admin`, `Doctor`, `Receptionist`, and `Patient` roles. It does not create an Admin user.

To apply migrations manually:

```powershell
dotnet ef database update --project HospitalCare.Infrastructure --startup-project HospitalCare.API
```

To create a migration:

```powershell
dotnet ef migrations add MigrationName --project HospitalCare.Infrastructure --startup-project HospitalCare.API --output-dir Persistence/Migrations
```

## Run

From the repository root:

```powershell
dotnet build HospitalCare.slnx
dotnet run --project HospitalCare.API --launch-profile http
```

The HTTP launch profile uses the Development environment and opens Swagger UI at `http://localhost:5074/swagger`.
