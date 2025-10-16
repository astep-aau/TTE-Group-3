using FluentValidation;

namespace translator_service.Features.GetRoute;

public class GetRouteValidator : AbstractValidator<GetRouteCommand>
{
    public GetRouteValidator()
    {
        RuleFor(x => x.CorrelationId)
            .NotEmpty()
            .WithMessage("CorrelationId is required.");

        RuleFor(x => x.Origin)
            .NotEmpty()
            .WithMessage("Origin is required.")
            .MinimumLength(2)
            .WithMessage("Origin must be at least 2 characters long.");

        RuleFor(x => x.Destination)
            .NotEmpty()
            .WithMessage("Destination is required.")
            .MinimumLength(2)
            .WithMessage("Destination must be at least 2 characters long.");
    }
}