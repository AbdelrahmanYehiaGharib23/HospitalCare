using System.Text.Json;
using FluentValidation;
using HospitalCare.Application.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace HospitalCare.API.ExceptionHandlers;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problemDetails = CreateProblemDetails(
            exception,
            httpContext.Request.Path.ToString());
        httpContext.Response.StatusCode =
            problemDetails.Status ?? StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/problem+json";

        await JsonSerializer.SerializeAsync(
            httpContext.Response.Body,
            problemDetails,
            JsonSerializerOptions.Web,
            cancellationToken);

        return true;
    }

    private static ProblemDetails CreateProblemDetails(
        Exception exception,
        string instance)
    {
        var (status, title, detail) = exception switch
        {
            NotFoundException => (
                StatusCodes.Status404NotFound,
                "Not Found",
                exception.Message),
            ForbiddenException => (
                StatusCodes.Status403Forbidden,
                "Forbidden",
                exception.Message),
            ConflictException => (
                StatusCodes.Status409Conflict,
                "Conflict",
                exception.Message),
            BusinessRuleException => (
                StatusCodes.Status400BadRequest,
                "Bad Request",
                exception.Message),
            ValidationException => (
                StatusCodes.Status400BadRequest,
                "Bad Request",
                null),
            UnauthorizedAccessException => (
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                exception.Message),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                "An unexpected error occurred.")
        };

        var problemDetails = new ProblemDetails
        {
            Type = "about:blank",
            Title = title,
            Status = status,
            Detail = detail,
            Instance = instance
        };

        if (exception is ValidationException validationException)
        {
            problemDetails.Extensions["errors"] = validationException.Errors
                .GroupBy(error => error.PropertyName)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(error => error.ErrorMessage).ToArray());
        }

        return problemDetails;
    }
}
