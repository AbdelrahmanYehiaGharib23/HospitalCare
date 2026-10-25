using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using HospitalCare.API.ExceptionHandlers;
using HospitalCare.Application.Common.Exceptions;
using Microsoft.AspNetCore.Http;

namespace HospitalCare.API.Tests.ExceptionHandlers;

public sealed class GlobalExceptionHandlerTests
{
    [Theory]
    [InlineData("not-found", 404, "Not Found")]
    [InlineData("forbidden", 403, "Forbidden")]
    [InlineData("conflict", 409, "Conflict")]
    [InlineData("business-rule", 400, "Bad Request")]
    [InlineData("unauthorized", 401, "Unauthorized")]
    [InlineData("unexpected", 500, "Internal Server Error")]
    public async Task TryHandleAsync_KnownException_ReturnsProblemDetails(
        string exceptionName,
        int expectedStatus,
        string expectedTitle)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/test";
        context.Response.Body = new MemoryStream();

        var handled = await new GlobalExceptionHandler().TryHandleAsync(
            context,
            CreateException(exceptionName),
            TestContext.Current.CancellationToken);

        Assert.True(handled);
        Assert.Equal(expectedStatus, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);

        using var problem = await ReadProblemAsync(context.Response.Body);
        Assert.Equal("about:blank", problem.RootElement.GetProperty("type").GetString());
        Assert.Equal(expectedTitle, problem.RootElement.GetProperty("title").GetString());
        Assert.Equal(expectedStatus, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("/api/test", problem.RootElement.GetProperty("instance").GetString());
    }

    [Fact]
    public async Task TryHandleAsync_ValidationException_IncludesFieldErrors()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var exception = new ValidationException(
        [
            new ValidationFailure("Email", "Email is invalid."),
            new ValidationFailure("Email", "Email is required.")
        ]);

        await new GlobalExceptionHandler().TryHandleAsync(
            context,
            exception,
            TestContext.Current.CancellationToken);

        using var problem = await ReadProblemAsync(context.Response.Body);
        var emailErrors = problem.RootElement
            .GetProperty("errors")
            .GetProperty("Email");

        Assert.Equal(400, context.Response.StatusCode);
        Assert.Equal(2, emailErrors.GetArrayLength());
    }

    private static Exception CreateException(string exceptionName)
    {
        return exceptionName switch
        {
            "not-found" => new NotFoundException("The resource was not found."),
            "forbidden" => new ForbiddenException("The operation is forbidden."),
            "conflict" => new ConflictException("The resource conflicts with existing data."),
            "business-rule" => new BusinessRuleException("The operation violates a business rule."),
            "unauthorized" => new UnauthorizedAccessException("Authentication is required."),
            _ => new InvalidOperationException("Internal exception details.")
        };
    }

    private static Task<JsonDocument> ReadProblemAsync(Stream body)
    {
        body.Position = 0;
        return JsonDocument.ParseAsync(body);
    }
}
