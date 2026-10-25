using HospitalCare.Application.Features.Departments.Queries;

namespace HospitalCare.Application.Tests.Departments;

public sealed class GetDepartmentsQueryValidatorTests
{
    private readonly GetDepartmentsQueryValidator _validator = new();

    [Fact]
    public void Validate_DefaultPaging_Succeeds()
    {
        var result = _validator.Validate(new GetDepartmentsQuery());

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void Validate_InvalidPaging_Fails(int pageNumber, int pageSize)
    {
        var result = _validator.Validate(
            new GetDepartmentsQuery(pageNumber, pageSize));

        Assert.False(result.IsValid);
    }
}
