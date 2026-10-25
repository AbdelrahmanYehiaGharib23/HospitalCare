using HospitalCare.Application.Common.Interfaces;
using HospitalCare.Application.Features.Appointments.Commands;
using HospitalCare.Domain.Entities;
using HospitalCare.Domain.Enums;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HospitalCare.Infrastructure.BackgroundServices;

public sealed class NoShowProcessor : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<NoShowProcessor> _logger;

    public NoShowProcessor(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        ILogger<NoShowProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        try
        {
            while (true)
            {
                await Task.Delay(PollInterval, _timeProvider, stoppingToken);
                await ProcessDueAppointmentsAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task ProcessDueAppointmentsAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider
                .GetRequiredService<IUnitOfWork>();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var cutoff = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(
                -Appointment.NoShowGracePeriodMinutes);
            var dueAppointments = await unitOfWork.Appointments.FindAsync(
                appointment =>
                    appointment.Status == AppointmentStatus.Queued &&
                    appointment.CalledAt.HasValue &&
                    appointment.CalledAt.Value <= cutoff &&
                    !appointment.CheckedInAt.HasValue,
                withTracking: false,
                cancellationToken);

            foreach (var appointment in dueAppointments)
            {
                await sender.Send(
                    new ProcessNoShowSwapCommand(appointment.Id),
                    cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                "No-show processing cycle failed with {ExceptionType}.",
                exception.GetType().Name);
        }
    }
}
