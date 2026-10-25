using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HospitalCare.API.Tests.Infrastructure;
using HospitalCare.Application.Common.Constants;
using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Application.Features.Patients.Commands;
using HospitalCare.Application.Features.Patients.DTOs;
using HospitalCare.Domain.Entities;
using HospitalCare.Domain.Enums;
using HospitalCare.Infrastructure.Persistence.DbInitializer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalCare.API.Tests.Patients;

public sealed class PatientMedicalHistoryControllerTests
{
    [Fact]
    public async Task GetPatientMedicalHistory_DoctorHasAppointment_ReturnsHistory()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        var patient = await SeedPatientAsync(factory, hasAppointment: true);
        Authenticate(factory, client, Roles.Doctor, doctorId: patient.DoctorId);

        using var response = await client.GetAsync(
            $"/api/patients/{patient.PatientId}/medical-history",
            TestContext.Current.CancellationToken);
        var result = await response.Content
            .ReadFromJsonAsync<PatientMedicalHistoryDto>(
                TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(patient.PatientId, result.PatientId);
        Assert.Equal("Penicillin", result.Allergies);
        Assert.Equal("Asthma", result.ChronicConditions);
    }

    [Fact]
    public async Task GetPatientMedicalHistory_DoctorHasNoAppointment_ReturnsForbidden()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        var patient = await SeedPatientAsync(factory, hasAppointment: false);
        Authenticate(factory, client, Roles.Doctor, doctorId: patient.DoctorId);

        using var response = await client.GetAsync(
            $"/api/patients/{patient.PatientId}/medical-history",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetPatientMedicalHistory_ReceptionistCanReadAnyPatient_ReturnsHistory()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        var patient = await SeedPatientAsync(factory, hasAppointment: false);
        Authenticate(factory, client, Roles.Receptionist);

        using var response = await client.GetAsync(
            $"/api/patients/{patient.PatientId}/medical-history",
            TestContext.Current.CancellationToken);
        var result = await response.Content
            .ReadFromJsonAsync<PatientMedicalHistoryDto>(
                TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal("Penicillin", result.Allergies);
        Assert.Equal("Asthma", result.ChronicConditions);
    }

    [Fact]
    public async Task GetMyMedicalHistory_PatientReadsOwnHistory()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        var patient = await SeedPatientAsync(factory, hasAppointment: false);
        Authenticate(
            factory,
            client,
            Roles.Patient,
            patientId: patient.PatientId);

        using var response = await client.GetAsync(
            "/api/patients/me/medical-history",
            TestContext.Current.CancellationToken);
        var result = await response.Content
            .ReadFromJsonAsync<PatientMedicalHistoryDto>(
                TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(patient.PatientId, result.PatientId);
        Assert.Equal("Penicillin", result.Allergies);
        Assert.Equal("Asthma", result.ChronicConditions);
    }

    [Fact]
    public async Task UpdateMyProfile_PatientChangesOwnMedicalHistory()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        var patient = await SeedPatientAsync(factory, hasAppointment: false);
        Authenticate(
            factory,
            client,
            Roles.Patient,
            patientId: patient.PatientId);

        using var updateResponse = await client.PutAsJsonAsync(
            "/api/patients/me",
            new UpdateMyPatientProfileCommand(
                "Updated Patient",
                "01000000001",
                new DateOnly(1990, 1, 1),
                "Latex",
                "Diabetes"),
            TestContext.Current.CancellationToken);
        using var historyResponse = await client.GetAsync(
            "/api/patients/me/medical-history",
            TestContext.Current.CancellationToken);
        var history = await historyResponse.Content
            .ReadFromJsonAsync<PatientMedicalHistoryDto>(
                TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        Assert.NotNull(history);
        Assert.Equal("Latex", history.Allergies);
        Assert.Equal("Diabetes", history.ChronicConditions);
    }

    [Fact]
    public async Task GetPatientMedicalHistory_AdminRequest_ReturnsForbidden()
    {
        using var factory = new HospitalCareApiFactory();
        using var client = factory.CreateClient();
        var patient = await SeedPatientAsync(factory, hasAppointment: true);
        Authenticate(factory, client, Roles.Admin);

        using var response = await client.GetAsync(
            $"/api/patients/{patient.PatientId}/medical-history",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task<SeededPatient> SeedPatientAsync(
        HospitalCareApiFactory factory,
        bool hasAppointment)
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var dbContext = services.GetRequiredService<ApplicationDbContext>();
        var hospitalLocalNow = TimeZoneInfo.ConvertTime(
            services.GetRequiredService<TimeProvider>().GetUtcNow(),
            services.GetRequiredService<TimeZoneInfo>());
        var patient = new Patient
        {
            UserId = $"patient-user-{Guid.NewGuid():N}",
            Name = "Medical History Patient",
            Email = $"medical-history-{Guid.NewGuid():N}@example.com",
            Phone = "01000000000",
            DateOfBirth = new DateOnly(1990, 1, 1),
            Allergies = "Penicillin",
            ChronicConditions = "Asthma"
        };
        var department = new Department
        {
            Name = $"Department-{Guid.NewGuid():N}"
        };
        var doctor = new Doctor
        {
            Name = $"Doctor-{Guid.NewGuid():N}",
            Department = department
        };
        var appointmentType = new AppointmentType
        {
            Name = $"Consultation-{Guid.NewGuid():N}",
            DurationMinutes = 30,
            BookingFee = 100
        };
        dbContext.AddRange(patient, doctor, appointmentType);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        if (hasAppointment)
        {
            dbContext.Appointments.Add(new Appointment
            {
                DoctorId = doctor.Id,
                PatientId = patient.Id,
                AppointmentTypeId = appointmentType.Id,
                AppointmentDate = DateOnly.FromDateTime(
                    hospitalLocalNow.DateTime),
                QueuePosition = 1,
                Status = AppointmentStatus.Completed
            });
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return new SeededPatient(patient.Id, doctor.Id);
    }

    private static void Authenticate(
        HospitalCareApiFactory factory,
        HttpClient client,
        string role,
        int? patientId = null,
        int? doctorId = null)
    {
        using var scope = factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider
            .GetRequiredService<IJwtTokenService>();
        var token = tokenService.GenerateToken(
            $"{role.ToLowerInvariant()}-user",
            $"{role.ToLowerInvariant()}@example.com",
            [role],
            patientId: patientId?.ToString(),
            doctorId: doctorId?.ToString()).Token;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    private sealed record SeededPatient(int PatientId, int DoctorId);
}
