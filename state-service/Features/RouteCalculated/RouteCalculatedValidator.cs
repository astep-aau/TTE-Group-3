using FluentValidation;

namespace StateService.Features.RouteCalculated
{
    public class RouteCalculatedMessageValidator : AbstractValidator<RouteCalculatedMessage>
    {
        public RouteCalculatedMessageValidator()
        {
            RuleFor(x => x.Pid).GreaterThan(0);
            RuleFor(x => x.RouteJson).NotEmpty();
            RuleFor(x => x.CorrelationId).MaximumLength(64);
        }
    }
}
