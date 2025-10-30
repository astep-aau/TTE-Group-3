using FluentValidation;

namespace StateService.Features.EstimationRequested
{
    public class EstimationRequestedMessageValidator : AbstractValidator<EstimationRequestedMessage>
    {
        public EstimationRequestedMessageValidator()
        {
            RuleFor(x => x.Start).NotEmpty().MaximumLength(256);
            RuleFor(x => x.End).NotEmpty().MaximumLength(256);
            RuleFor(x => x.TravelTimeUtc).GreaterThan(DateTime.UtcNow.AddYears(-1));
            RuleFor(x => x.CorrelationId).MaximumLength(64);
        }
    }
}
