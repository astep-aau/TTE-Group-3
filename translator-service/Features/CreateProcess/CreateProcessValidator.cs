using FluentValidation;
using translator_service.Features.CreateProcess;

namespace translator_service.Features.CreateProcess;

public class CreateProcessValidator : AbstractValidator<CreateProcessCommand>
{
    public CreateProcessValidator()
    {
        RuleFor(x => x.CorrelationId)
            .NotEmpty().WithMessage("CorrelationId is required");

        RuleFor(x => x.Origin)
            .NotEmpty().WithMessage("Origin is required")
            .MinimumLength(2).WithMessage("Origin must be at least 2 characters long");

        RuleFor(x => x.Destination)
            .NotEmpty().WithMessage("Destination is required")
            .MinimumLength(2).WithMessage("Destination must be at least 2 characters long");

        RuleFor(x => x.CreatedAt)
            .LessThanOrEqualTo(_ => DateTime.UtcNow)
            .WithMessage("CreatedAt must be in the past or now");
    }
}
















