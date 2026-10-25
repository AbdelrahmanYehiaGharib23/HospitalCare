using HospitalCare.Application.Features.Doctors.Queries;

namespace HospitalCare.Application.Tests.Doctors;

public sealed class DoctorQueryValidatorTests
{
    [Fact]
    public void GetDoctorsByDepartmentQuery_DefaultPaging_Succeeds()
    {
        var validator = new GetDoctorsByDepartmentQueryValidator();

        var result = validator.Validate(new GetDoctorsByDepartmentQuery());

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0, 1, 20)]
    [InlineData(null, 0, 20)]
    [InlineData(null, 1, 101)]
    public void GetDoctorsByDepartmentQuery_InvalidValues_Fails(
        int? departmentId,
        int pageNumber,
        int pageSize)
    {
        var validator = new GetDoctorsByDepartmentQueryValidator();

        var result = validator.Validate(
            new GetDoctorsByDepartmentQuery(
                departmentId,
                PageNumber: pageNumber,
                PageSize: pageSize));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void GetDoctorByIdQuery_InvalidId_Fails()
    {
        var validator = new GetDoctorByIdQueryValidator();

        var result = validator.Validate(new GetDoctorByIdQuery(0));

        Assert.False(result.IsValid);
    }
}
