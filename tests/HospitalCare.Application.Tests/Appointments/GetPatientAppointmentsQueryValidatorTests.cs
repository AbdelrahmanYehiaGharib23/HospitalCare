using HospitalCare.Application.Features.Appointments.Queries;

namespace HospitalCare.Application.Tests.Appointments;

public sealed class GetPatientAppointmentsQueryValidatorTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 100)]
    public void Validate_ValidPage_Succeeds(int pageNumber, int pageSize)
    {
        var validator = new GetPatientAppointmentsQueryValidator();

        var result = validator.Validate(
            new GetPatientAppointmentsQuery(pageNumber, pageSize));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void Validate_InvalidPage_Fails(int pageNumber, int pageSize)
    {
        var validator = new GetPatientAppointmentsQueryValidator();

        var result = validator.Validate(
            new GetPatientAppointmentsQuery(pageNumber, pageSize));

        Assert.False(result.IsValid);
    }
}
