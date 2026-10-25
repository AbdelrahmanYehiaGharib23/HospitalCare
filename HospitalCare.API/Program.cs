using System.Text.Json;
using HospitalCare.API.ExceptionHandlers;
using HospitalCare.Application;
using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi;
using Serilog;
using Serilog.Events;
using System.Threading.RateLimiting;

namespace HospitalCare.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Host.UseSerilog((context, configuration) =>
                configuration
                    .MinimumLevel.Information()
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                    // Exception messages may contain patient data; request logs retain status and duration.
                    .MinimumLevel.Override(
                        "Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware",
                        LogEventLevel.Fatal)
                    .Enrich.FromLogContext()
                    .WriteTo.Console()
                    .WriteTo.File(
                        context.Configuration["Serilog:FilePath"] ??
                        "logs/hospitalcare-.log",
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 14));

            // Add services to the container.

            builder.Services.AddControllers();
            builder.Services.AddProblemDetails();
            builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
            builder.Services.AddRateLimiter(options =>
            {
                options.AddPolicy(
                    RateLimitingPolicies.Login,
                    context => RateLimitPartition.GetFixedWindowLimiter(
                        context.Connection.RemoteIpAddress?.ToString() ??
                        "unknown",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 10,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0,
                            AutoReplenishment = true
                        }));

                options.OnRejected = async (context, cancellationToken) =>
                {
                    var response = context.HttpContext.Response;
                    response.StatusCode = StatusCodes.Status429TooManyRequests;
                    response.ContentType = "application/problem+json";

                    await JsonSerializer.SerializeAsync(
                        response.Body,
                        new ProblemDetails
                        {
                            Type = "about:blank",
                            Title = "Too Many Requests",
                            Status = StatusCodes.Status429TooManyRequests,
                            Detail = "Too many login attempts. Try again later.",
                            Instance = context.HttpContext.Request.Path
                                .ToString()
                        },
                        JsonSerializerOptions.Web,
                        cancellationToken);
                };
            });
            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi(options =>
            {
                options.AddDocumentTransformer(
                    (document, context, cancellationToken) =>
                    {
                        document.Components ??= new OpenApiComponents();

                        document.Components.SecuritySchemes ??=
                            new Dictionary<string, IOpenApiSecurityScheme>();

                        document.Components.SecuritySchemes["Bearer"] =
                            new OpenApiSecurityScheme
                            {
                                Type = SecuritySchemeType.Http,
                                Scheme = "bearer",
                                BearerFormat = "JWT",
                                Description = "Enter your JWT Bearer token."
                            };

                        return Task.CompletedTask;
                    });

                options.AddOperationTransformer(
                    (operation, context, cancellationToken) =>
                    {
                        var endpointMetadata = context.Description
                            .ActionDescriptor.EndpointMetadata;

                        if (endpointMetadata.OfType<IAuthorizeData>().Any() &&
                            !endpointMetadata.OfType<AllowAnonymousAttribute>().Any())
                        {
                            operation.Security ??= [];
                            operation.Security.Add(
                                new OpenApiSecurityRequirement
                                {
                                    [new OpenApiSecuritySchemeReference(
                                        "Bearer",
                                        context.Document)] = []
                                });
                        }

                        return Task.CompletedTask;
                    });
            });
            builder.Services.AddApplication();
            // Infrastructure
            builder.Services.AddInfrastructure(builder.Configuration);

            var app = builder.Build();
            app.UseSerilogRequestLogging();
            app.UseExceptionHandler();

            using (var scope = app.Services.CreateScope())
            {
                var initializer = scope.ServiceProvider
                    .GetRequiredService<IDbInitializer>();

                initializer.Initialize();
            }

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();

                app.UseSwaggerUI(options =>
                {
                    options.SwaggerEndpoint(
                        "/openapi/v1.json",
                        "HospitalCare API v1");
                });
            }


            app.UseHttpsRedirection();

            app.UseRouting();
            app.UseRateLimiter();
            app.UseAuthentication();
            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
