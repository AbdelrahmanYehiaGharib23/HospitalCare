using FluentValidation;
using HospitalCare.Application.Common.Behaviors;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalCare.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(
        this IServiceCollection services)
        {
            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(
                    typeof(DependencyInjection).Assembly);

                cfg.AddOpenBehavior(
                    typeof(LoggingBehavior<,>));

                cfg.AddOpenBehavior(
                    typeof(ValidationBehavior<,>));
            });

            services.AddValidatorsFromAssembly(
                typeof(DependencyInjection).Assembly);

            return services;
        }
    }
}
