using System.Security.Claims;
using HospitalCare.Infrastructure.Identity;
using Microsoft.AspNetCore.Http;

namespace HospitalCare.API.Tests.Doctors;

public sealed class CurrentUserServiceTests
{
    [Fact]
    public void DoctorId_DoctorClaimExists_ReturnsClaimValue()
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    [new Claim("doctorId", "42")],
                    "Bearer"))
        };
        var currentUserService = new CurrentUserService(
            new HttpContextAccessor { HttpContext = httpContext });

        Assert.Equal("42", currentUserService.DoctorId);
    }
}
