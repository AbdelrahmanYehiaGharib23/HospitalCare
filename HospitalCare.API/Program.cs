using HospitalCare.Application;
using HospitalCare.Application.Common.Interfaces;
using HospitalCare.API.ExceptionHandlers;
using HospitalCare.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;

namespace HospitalCare.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            builder.Services.AddProblemDetails();
            builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
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

            app.UseAuthentication();
            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
