# Docker and CI

## Run the API with Docker

Install Docker Desktop with Compose support. Copy the placeholder environment file and replace its dummy secrets with private local values:

```powershell
Copy-Item .env.example .env
```

The API and SQL Server run together. The API waits for the SQL Server health check, then applies EF Core migrations and seeds roles, appointment types, and the optional Admin account at startup.

```powershell
docker compose up --build
```

The container exposes the API at <http://localhost:8080>. The Compose configuration sets `ASPNETCORE_ENVIRONMENT=Development`, which enables Swagger UI at <http://localhost:8080/swagger>.

In Swagger, register a patient with `POST /api/auth/register`, then log in with `POST /api/auth/login`. Use the returned JWT with Swagger's **Authorize** button to call protected endpoints. A valid sample registration body is:

```json
{
  "name": "Example Patient",
  "email": "patient@example.test",
  "password": "LocalDemo!123",
  "phone": "+201000000000",
  "dateOfBirth": "1990-01-01",
  "allergies": "",
  "chronicConditions": ""
}
```

The local default payment provider is `Fake`; it avoids Paymob credentials and returns a non-routable demo checkout URL. To use Paymob, set `PAYMENT_GATEWAY_PROVIDER=Paymob` in `.env` and provide real Paymob secret, public, HMAC, integration ID, and public HTTPS notification URL values. Do not put real values in tracked files.

Stop the services with `docker compose down`. Add `-v` only when you also want to delete the named SQL Server data volume:

```powershell
docker compose down -v
```

## Run tests locally

The test projects use xUnit v3. The API test host runs in Development, so local User Secrets that select `PaymentGateway:Provider=Paymob` can make API integration tests contact your Paymob test account. Keep those credentials in User Secrets. Automated API tests should force Fake for deterministic, offline checkout; set `$env:PaymentGateway__Provider = "Fake"` before running them. This only overrides the test process and does not change User Secrets or your local API configuration. `PaymobPaymentGatewayTests` use stub HTTP handlers. The repository's `global.json` selects the Microsoft.Testing.Platform runner, so use its xUnit TRX reporter flags instead of the VSTest `--logger trx` option:

```powershell
dotnet restore HospitalCare.slnx
dotnet build HospitalCare.slnx -c Release --no-restore
$env:PaymentGateway__Provider = "Fake"
dotnet test tests/HospitalCare.API.Tests/HospitalCare.API.Tests.csproj -c Release --results-directory tests/HospitalCare.API.Tests/TestResults --report-xunit-trx --report-xunit-trx-filename api-tests.trx
dotnet test tests/HospitalCare.Application.Tests/HospitalCare.Application.Tests.csproj -c Release --results-directory tests/HospitalCare.Application.Tests/TestResults --report-xunit-trx --report-xunit-trx-filename application-tests.trx
```

The test results are written under each test project's `TestResults` directory.

## GitHub Actions

The `CI` workflow runs on pushes and pull requests targeting `main`. It restores packages with a NuGet cache, builds the solution in Release, runs both test projects with the MTP TRX reporter, and uploads TRX results. A second job builds the Docker image without pushing it. The workflow uses read-only repository permissions and does not deploy.

[![CI](https://github.com/AbdelrahmanYehiaGharib23/HospitalCare/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/AbdelrahmanYehiaGharib23/HospitalCare/actions/workflows/ci.yml)