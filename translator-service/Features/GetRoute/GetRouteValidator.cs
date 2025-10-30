using FluentValidation;

namespace translator_service.Features.GetRoute;

public class GetRouteValidator : AbstractValidator<GetRouteCommand>
{
    public GetRouteValidator()
    {
        RuleFor(x => x.CorrelationId)
            .NotEmpty()
            .WithMessage("CorrelationId is required.");
        // Origin and Destination are no longer required when querying by correlation id
    }
}