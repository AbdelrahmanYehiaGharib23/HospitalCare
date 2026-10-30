FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore from project metadata first so source edits do not invalidate the NuGet layer.
COPY HospitalCare.slnx global.json ./
COPY .gitignore ./
COPY HospitalCare.API/HospitalCare.API.csproj HospitalCare.API/
COPY HospitalCare.Application/HospitalCare.Application.csproj HospitalCare.Application/
COPY HospitalCare.Domain/HospitalCare.Domain.csproj HospitalCare.Domain/
COPY HospitalCare.Infrastructure/HospitalCare.Infrastructure.csproj HospitalCare.Infrastructure/
COPY tests/HospitalCare.API.Tests/HospitalCare.API.Tests.csproj tests/HospitalCare.API.Tests/
COPY tests/HospitalCare.Application.Tests/HospitalCare.Application.Tests.csproj tests/HospitalCare.Application.Tests/
RUN dotnet restore HospitalCare.slnx

COPY . .
RUN dotnet publish HospitalCare.API/HospitalCare.API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build --chown=app:app /app/publish .
RUN mkdir -p /app/logs && chown app:app /app/logs
USER app
ENTRYPOINT ["dotnet", "HospitalCare.API.dll"]