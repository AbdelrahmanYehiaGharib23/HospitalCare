using FluentValidation;

namespace HospitalCare.Application.Features.DoctorSchedules.Commands;

public sealed class UpdateDoctorScheduleCommandValidator
    : AbstractValidator<UpdateDoctorScheduleCommand>
{
    public UpdateDoctorScheduleCommandValidator()
    {
        RuleFor(command => command.Ranges)
            .NotNull();

        RuleForEach(command => command.Ranges)
            .ChildRules(range =>
            {
                range.RuleFor(item => item.DayOfWeek)
                    .IsInEnum();

                range.RuleFor(item => item.EndTime)
                    .GreaterThan(item => item.StartTime);
            });

        RuleFor(command => command.Ranges)
            .Custom((ranges, context) =>
            {
                if (ranges is null)
                {
                    return;
                }

                foreach (var dayRanges in ranges.GroupBy(range => range.DayOfWeek))
                {
                    var orderedRanges = dayRanges
                        .OrderBy(range => range.StartTime)
                        .ToArray();

                    for (var index = 1; index < orderedRanges.Length; index++)
                    {
                        if (orderedRanges[index].StartTime <
                            orderedRanges[index - 1].EndTime)
                        {
                            context.AddFailure(
                                nameof(UpdateDoctorScheduleCommand.Ranges),
                                "Schedule ranges on the same day cannot overlap.");
                            break;
                        }
                    }
                }
            });
    }
}
