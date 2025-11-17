using System;
using System.Globalization;
using FluentValidation;
using RouteEstimationService.Domain.Entities.Events;

namespace RouteEstimationService.Features.CreateRoute;

public class CreateRouteValidator : AbstractValidator<CreateProcessEvent>
{
    public CreateRouteValidator()
    {
        RuleFor(x => x.ProcessId).GreaterThan(0).WithMessage("[Validator] ProcessId must be greater than 0");
        RuleFor(x => x.CorrelationId).NotEmpty().WithMessage("[Validator] CorrelationId is required");
        RuleFor(x => x.Origin)
            .NotEmpty().WithMessage("[Validator] Origin must be specified")
            .Must(BeLatLon).WithMessage("[Validator] Origin must be in 'lat,lon' format with numeric values");
        RuleFor(x => x.Destination)
            .NotEmpty().WithMessage("[Validator] Destination must be specified")
            .Must(BeLatLon).WithMessage("[Validator] Destination must be in 'lat,lon' format with numeric values");
        RuleFor(x => x.Origin).NotEqual(x => x.Destination).WithMessage("[Validator] Origin and Destination must be different");
        RuleFor(x => x.CreatedAt).NotEqual(default(DateTime)).WithMessage("[Validator] CreatedAt must be set");
        RuleFor(x => x.ModelVersion).NotEmpty().WithMessage("[Validator] ModelVersion must be specified");
    }

    private static bool BeLatLon(string? s)
    {
        string[] parts = s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2) return false;
        return double.TryParse(parts[0], NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out _)
               && double.TryParse(parts[1], NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out _);
    }
}