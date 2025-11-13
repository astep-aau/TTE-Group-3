using translator_service.Features.GetRoute;
using Xunit;

namespace TestTranslatorService;

public class GetRouteValidatorTest
{
    private readonly GetRouteValidator _validator = new();

    [Fact]
    public void Validate_ReturnsValid_WhenCorrelationIdProvided()
    {
        // Arrange
        var command = new GetRouteCommand(Guid.NewGuid());

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ReturnsError_WhenCorrelationIdMissing()
    {
        // Arrange
        var command = new GetRouteCommand(Guid.Empty);

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal("CorrelationId is required.", error.ErrorMessage);
        Assert.Equal(nameof(GetRouteCommand.CorrelationId), error.PropertyName);
    }
}

