using System;
using System.Collections.Generic;
using System.Linq;
using StateService.Features.RouteEstimationCompleted;
using Xunit;

namespace StateService.Tests;

public class RouteEstimationCompletedMessageValidatorTests
{
    private readonly RouteEstimationCompletedMessageValidator _validator = new();

    [Fact]
    public void Validate_WithValidMessage_ShouldPass()
    {
        var message = new RouteEstimationCompletedMessage(
            Guid.NewGuid(),
            "Copenhagen",
            "Odense",
            145.2,
            95.3,
            new List<RouteCoordinate> { new(55.6761, 12.5683) });

        var result = _validator.Validate(message);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WithEmptyOrigin_ShouldFail()
    {
        var message = new RouteEstimationCompletedMessage(
            Guid.NewGuid(),
            string.Empty,
            "Odense",
            145.2,
            95.3,
            new List<RouteCoordinate> { new(55.6761, 12.5683) });

        var result = _validator.Validate(message);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RouteEstimationCompletedMessage.Origin));
    }

    [Fact]
    public void Validate_WithNonPositiveDistance_ShouldFail()
    {
        var message = new RouteEstimationCompletedMessage(
            Guid.NewGuid(),
            "Copenhagen",
            "Odense",
            0,
            95.3,
            new List<RouteCoordinate> { new(55.6761, 12.5683) });

        var result = _validator.Validate(message);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RouteEstimationCompletedMessage.DistanceKm));
    }
}

