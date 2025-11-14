using System;
using StateService.Features.EstimationRequested;
using Xunit;

namespace StateService.Tests;

public class EstimationRequestedMessageValidatorTests
{
    private readonly EstimationRequestedMessageValidator _validator = new();

    [Fact]
    public void Validate_WithValidMessage_ShouldPass()
    {
        var message = new EstimationRequestedMessage(
            1,
            "A",
            "B",
            DateTime.UtcNow.AddHours(2),
            "corr-123");

        var result = _validator.Validate(message);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WithEmptyStart_ShouldFail()
    {
        var message = new EstimationRequestedMessage(
            1,
            string.Empty,
            "B",
            DateTime.UtcNow.AddHours(2),
            "corr-123");

        var result = _validator.Validate(message);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_WithOldTravelTime_ShouldFail()
    {
        var message = new EstimationRequestedMessage(
            1,
            "A",
            "B",
            DateTime.UtcNow.AddYears(-2),
            "corr-123");

        var result = _validator.Validate(message);

        Assert.False(result.IsValid);
    }
}

