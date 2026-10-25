using HospitalCare.Application.Features.AppointmentTypes.Queries;

namespace HospitalCare.Application.Tests.AppointmentTypes;

public sealed class GetAppointmentTypesQueryValidatorTests
{
    private readonly GetAppointmentTypesQueryValidator _validator = new();

    [Fact]
    public void Validate_DefaultPaging_Succeeds()
    {
        var result = _validator.Validate(new GetAppointmentTypesQuery());

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void Validate_InvalidPaging_Fails(int pageNumber, int pageSize)
    {
        var result = _validator.Validate(
            new GetAppointmentTypesQuery(pageNumber, pageSize));

        Assert.False(result.IsValid);
    }
}
