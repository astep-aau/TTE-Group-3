using StateService.Features.ProcessFinished;
using Xunit;

namespace StateService.Tests;

public class ProcessFinishedMessageValidatorTests
{
    private readonly ProcessFinishedMessageValidator _validator = new();

    [Fact]
    public void Validate_WithValidMessage_ShouldPass()
    {
        var message = new ProcessFinishedMessage(5, "result", "corr-1");

        var result = _validator.Validate(message);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WithInvalidPid_ShouldFail()
    {
        var message = new ProcessFinishedMessage(0, "result", "corr-1");

        var result = _validator.Validate(message);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_WithEmptySummary_ShouldFail()
    {
        var message = new ProcessFinishedMessage(5, string.Empty, "corr-1");

        var result = _validator.Validate(message);

        Assert.False(result.IsValid);
    }
}

