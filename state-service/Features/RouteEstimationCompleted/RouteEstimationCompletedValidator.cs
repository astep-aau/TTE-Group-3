using System;
using FluentValidation;

namespace StateService.Features.RouteEstimationCompleted
{
    public class RouteEstimationCompletedMessageValidator : AbstractValidator<RouteEstimationCompletedMessage>
    {
        public RouteEstimationCompletedMessageValidator()
        {
            RuleFor(x => x.CorrelationId).NotEqual(Guid.Empty);
            RuleFor(x => x.Origin).NotEmpty();
            RuleFor(x => x.Destination).NotEmpty();
            RuleFor(x => x.DistanceKm).GreaterThan(0);
            RuleFor(x => x.TravelTimeMinutes).GreaterThan(0);
            RuleFor(x => x.Path).NotNull();
        }
    }
}

