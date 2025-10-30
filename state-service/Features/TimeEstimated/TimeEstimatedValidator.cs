using FluentValidation;

namespace StateService.Features.TimeEstimated
{
    public class TimeEstimatedMessageValidator : AbstractValidator<TimeEstimatedMessage>
    {
        public TimeEstimatedMessageValidator()
        {
            RuleFor(x => x.Pid).GreaterThan(0);
            RuleFor(x => x.Seconds).GreaterThan(0);
            RuleFor(x => x.ModelVersion).NotEmpty().MaximumLength(64);
            RuleFor(x => x.CorrelationId).MaximumLength(64);
        }
    }
}
