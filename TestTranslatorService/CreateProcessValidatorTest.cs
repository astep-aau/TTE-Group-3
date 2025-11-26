using Xunit;
using translator_service.Features.CreateProcess;

namespace TestTranslatorService;

public class CreateProcessValidatorTest
{
    private CreateProcessValidator _validator;
    
    public CreateProcessValidatorTest()
    {
        _validator = new CreateProcessValidator();
    }
    
    [Fact]
    public void TestValidCreateProcessCommand()
    {
        // Arrange
        var command = new CreateProcessCommand
        {
            Id = 1,
            CorrelationId = Guid.NewGuid(),
            Origin = "123.456789,987.654321",
            Destination = "987.654321,123.456789",
            CreatedAt = DateTime.UtcNow.AddMinutes(-1),
            ModelVersion = "1.0",
            TimeOfTravel = TimeOnly.FromDateTime(DateTime.Now)
        };

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void TestInvalidCreateProcessCommand()
    {
        // Arrange
        var command = new CreateProcessCommand
        {
            Id = 1,
            CorrelationId = Guid.Empty,
            Origin = "",
            Destination = "",
            CreatedAt = DateTime.UtcNow.AddMinutes(10),
            ModelVersion = "1.0",
            TimeOfTravel = TimeOnly.FromDateTime(DateTime.Now)
        };
        
        // Act
        var result = _validator.Validate(command);
        
        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => 
            e.PropertyName == "CorrelationId" && 
            e.ErrorMessage == "CorrelationId is required");
        Assert.Contains(result.Errors, e =>
            e.PropertyName == "Origin" &&
            e.ErrorMessage == "Origin is required");
        Assert.Contains(result.Errors, e =>
            e.PropertyName == "Destination" &&
            e.ErrorMessage == "Destination is required");
        Assert.Contains(result.Errors, e =>
            e.PropertyName == "CreatedAt" &&
            e.ErrorMessage == "CreatedAt must be in the past or now");
    }
}