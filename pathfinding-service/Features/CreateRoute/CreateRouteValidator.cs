using FluentValidation;
using PathfindingService.Features.CreateRoute;

namespace PathfindingService.Features.CreateRoute
{
    // public class CreateRouteValidator : AbstractValidator<CreateRouteCommand>
    // {
    //     public CreateRouteValidator()
    //     {
    //         RuleFor(x => x.Origin).NotEmpty().WithMessage("Origin must be specified");
    //         RuleFor(x => x.Destination).NotEmpty().WithMessage("Destination must be specified");
    //         RuleFor(x => x.CorrelationId).NotEmpty().WithMessage("CorrelationID is required");
    //         RuleFor(x => x.Origin).NotEqual(x => x.Destination).WithMessage("Origin and Destination must be different");
    //     }
    // }
}
