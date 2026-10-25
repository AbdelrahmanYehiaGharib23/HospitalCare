using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;

namespace HospitalCare.API.Tests.Infrastructure;

internal sealed class AuthorizationMatrixApiFactory : HospitalCareApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ISender>();
            services.AddSingleton<ISender>(
                _ => DispatchProxy.Create<ISender, SuccessfulSenderProxy>());
        });
    }
}

public class SuccessfulSenderProxy : DispatchProxy
{
    protected override object? Invoke(
        MethodInfo? targetMethod,
        object?[]? args)
    {
        var returnType = targetMethod?.ReturnType ??
            throw new InvalidOperationException();
        if (returnType.IsGenericType &&
            returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var resultType = returnType.GetGenericArguments()[0];
            var result = resultType.IsValueType
                ? Activator.CreateInstance(resultType)
                : null;
            return typeof(Task)
                .GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(resultType)
                .Invoke(null, [result]);
        }

        if (returnType == typeof(Task))
        {
            return Task.CompletedTask;
        }

        throw new InvalidOperationException(
            $"Unsupported sender method: {targetMethod.Name}.");
    }
}
