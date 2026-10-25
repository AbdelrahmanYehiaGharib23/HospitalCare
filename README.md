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

The payment provider is selected with `PaymentGateway:Provider`; the checked-in value uses the internal Fake provider. Store its webhook signing secret outside the repository:

```powershell
dotnet user-secrets set "PaymentGateway:Fake:WebhookSecret" "REPLACE_WITH_A_RANDOM_SECRET_OF_AT_LEAST_32_UTF8_BYTES" --project HospitalCare.API
```

The equivalent environment variable is `PaymentGateway__Fake__WebhookSecret`. The Fake provider returns a non-routable checkout URL and accepts signed test callbacks at `POST /api/payments/webhook`; it does not contact a payment service.

The webhook reads a raw UTF-8 JSON body with `gatewayReference`, `status` (`Succeeded` or `Failed`), `amount`, and `currency` (`EGP`). Send `X-Payment-Signature` as the hexadecimal HMAC-SHA256 of the exact request body using the configured secret. A successful callback creates the queued Appointment if capacity remains; otherwise the Fake refund completes locally.

Doctor schedules use local wall-clock time. Availability calculations use `Hospital:TimeZoneId`, which defaults to `Africa/Cairo`; the environment variable name is `Hospital__TimeZoneId`.

## Database

On startup, `DbInitializer` applies pending Entity Framework migrations and seeds the `Admin`, `Doctor`, `Receptionist`, and `Patient` roles.

The first Admin account is seeded only when both `SeedAdmin:Email` and `SeedAdmin:Password` are configured. Store them in user secrets or environment variables:

```powershell
dotnet user-secrets set "SeedAdmin:Email" "admin@example.com" --project HospitalCare.API
dotnet user-secrets set "SeedAdmin:Password" "REPLACE_WITH_A_STRONG_PASSWORD" --project HospitalCare.API
```

When neither value is configured, startup skips Admin account seeding. If only one value is configured, startup fails. If the configured email already belongs to an account without the Admin role, startup also fails rather than changing that account's permissions.

The equivalent environment variable names are `SeedAdmin__Email` and `SeedAdmin__Password`.

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
