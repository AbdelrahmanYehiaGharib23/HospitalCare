using System.Text;
using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Domain.Services;
using HospitalCare.Infrastructure.Identity;
using HospitalCare.Infrastructure.Authentication;
using HospitalCare.Infrastructure.Persistence;
using HospitalCare.Infrastructure.Persistence.DbInitializer;
using HospitalCare.Infrastructure.Payments;
using HospitalCare.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
namespace HospitalCare.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(
                    configuration.GetConnectionString("DefaultConnection")));

            services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IDbInitializer, DbInitializer>();
            services.AddScoped<AdminAccountSeeder>();
            services.AddScoped<AppointmentTypeSeeder>();
            services.AddSingleton<TimeProvider>(TimeProvider.System);
            services.AddSingleton<DoctorCapacityCalculator>();

            var paymentProvider = configuration["PaymentGateway:Provider"];
            if (!string.Equals(
                    paymentProvider,
                    "Fake",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "PaymentGateway:Provider must be configured as Fake.");
            }

            services.Configure<FakePaymentGatewayOptions>(
                configuration.GetSection(FakePaymentGatewayOptions.SectionName));
            services.AddSingleton<IPaymentGateway, FakePaymentGateway>();

            var timeZoneId = configuration["Hospital:TimeZoneId"];
            var hospitalTimeZone = TimeZoneInfo.FindSystemTimeZoneById(
                string.IsNullOrWhiteSpace(timeZoneId)
                    ? "Africa/Cairo"
                    : timeZoneId);
            services.AddSingleton(hospitalTimeZone);

            services.Configure<AdminAccountOptions>(
                configuration.GetSection(AdminAccountOptions.SectionName));

            services.Configure<JwtOptions>(
           configuration.GetSection(JwtOptions.SectionName));

            var jwtOptions = configuration
                .GetSection(JwtOptions.SectionName)
                .Get<JwtOptions>()
                ?? throw new InvalidOperationException(
                    "JWT configuration is missing.");

            if (string.IsNullOrWhiteSpace(jwtOptions.SecretKey) ||
                Encoding.UTF8.GetByteCount(jwtOptions.SecretKey) < 32)
            {
                throw new InvalidOperationException(
                    "JWT SecretKey must contain at least 32 UTF-8 bytes.");
            }

            if (string.IsNullOrWhiteSpace(jwtOptions.Issuer) ||
                string.IsNullOrWhiteSpace(jwtOptions.Audience))
            {
                throw new InvalidOperationException(
                    "JWT Issuer and Audience must be configured.");
            }

            services.AddIdentityCore<ApplicationUser>(options =>
                {
                    options.User.RequireUniqueEmail = true;

                    options.Password.RequiredLength = 8;
                    options.Password.RequireDigit = true;
                    options.Password.RequireUppercase = true;
                    options.Password.RequireLowercase = true;
                    options.Password.RequireNonAlphanumeric = true;
                })
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddSignInManager();

            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme =
                        JwtBearerDefaults.AuthenticationScheme;

                    options.DefaultChallengeScheme =
                        JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters =
                        new TokenValidationParameters
                        {
                            ValidateIssuer = true,
                            ValidIssuer = jwtOptions.Issuer,

                            ValidateAudience = true,
                            ValidAudience = jwtOptions.Audience,

                            ValidateIssuerSigningKey = true,
                            IssuerSigningKey = new SymmetricSecurityKey(
                                Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),

                            ValidateLifetime = true,
                            ClockSkew = TimeSpan.FromSeconds(30)
                        };
                });

            services.AddAuthorization();

            services.AddScoped<IJwtTokenService, JwtTokenService>();
            services.AddScoped<IIdentityService, IdentityService>();
            services.AddHttpContextAccessor();
            services.AddScoped<ICurrentUserService, CurrentUserService>();

            return services;
        }
    }
}
