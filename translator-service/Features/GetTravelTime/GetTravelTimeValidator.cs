using FluentValidation;

namespace translator_service.Features.GetTravelTime
{
    public class GetTravelTimeValidator : AbstractValidator<GetTravelTimeCommand>
    {
        public GetTravelTimeValidator()
        {
            RuleFor(x => x.CorrelationId)
                .NotEmpty().WithMessage("Correlation ID is required.");
        }
    }
}