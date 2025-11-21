using FluentAssertions;
using RouteEstimationService.Domain.Entities.Events;
using RouteEstimationService.Features.CreateRoute;

namespace TestRouteEstimationService;

public class CreateRouteValidatorTests
{
    private readonly CreateRouteValidator _validator = new();

    #region Happy Path - Valid Events

    [Fact]
    public async Task ValidateAsync_WithValidEvent_ShouldPass()
    {
        // Arrange
        var validEvent = new CreateProcessEvent
        {
            ProcessId = 1,
            CorrelationId = Guid.NewGuid(),
            Origin = "55.6761,12.5683",
            Destination = "55.6863,12.5700",
            TimeOfTravel = new TimeOnly(10, 30),
            CreatedAt = DateTime.UtcNow,
            ModelVersion = "v1.0"
        };

        // Act
        var result = await _validator.ValidateAsync(validEvent, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("0.0,0.0", "1.0,1.0")] // Zero coordinates
    [InlineData("-90.0,-180.0", "90.0,180.0")] // Extreme coordinates
    [InlineData("55.6761, 12.5683", "55.6863, 12.5700")] // With spaces
    [InlineData(" 55.6761 , 12.5683 ", " 55.6863 , 12.5700 ")] // Extra whitespace
    [InlineData("55.6761,12.5683,100", "55.6863,12.5700,200")] // With elevation (extra parts)
    public async Task ValidateAsync_WithVariousValidCoordinateFormats_ShouldPass(string origin, string destination)
    {
        // Arrange
        var validEvent = new CreateProcessEvent
        {
            ProcessId = 1,
            CorrelationId = Guid.NewGuid(),
            Origin = origin,
            Destination = destination,
            TimeOfTravel = new TimeOnly(10, 30),
            CreatedAt = DateTime.UtcNow,
            ModelVersion = "v1.0"
        };

        // Act
        var result = await _validator.ValidateAsync(validEvent, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeTrue($"'{origin}' and '{destination}' are valid coordinate formats");
        result.Errors.Should().BeEmpty();
    }

    #endregion

    #region ProcessId Validation

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    [InlineData(int.MinValue)]
    public async Task ValidateAsync_WithInvalidProcessId_ShouldFail(int invalidProcessId)
    {
        // Arrange
        var invalidEvent = new CreateProcessEvent
        {
            ProcessId = invalidProcessId,
            CorrelationId = Guid.NewGuid(),
            Origin = "55.6761,12.5683",
            Destination = "55.6863,12.5700",
            TimeOfTravel = new TimeOnly(10, 30),
            CreatedAt = DateTime.UtcNow,
            ModelVersion = "v1.0"
        };

        // Act
        var result = await _validator.ValidateAsync(invalidEvent, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => 
            e.PropertyName == nameof(CreateProcessEvent.ProcessId) &&
            e.ErrorMessage == "[Validator] ProcessId must be greater than 0");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(999999)]
    [InlineData(int.MaxValue)]
    public async Task ValidateAsync_WithValidProcessId_ShouldPass(int validProcessId)
    {
        // Arrange
        var validEvent = new CreateProcessEvent
        {
            ProcessId = validProcessId,
            CorrelationId = Guid.NewGuid(),
            Origin = "55.6761,12.5683",
            Destination = "55.6863,12.5700",
            TimeOfTravel = new TimeOnly(10, 30),
            CreatedAt = DateTime.UtcNow,
            ModelVersion = "v1.0"
        };

        // Act
        var result = await _validator.ValidateAsync(validEvent, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region CorrelationId Validation

    [Fact]
    public async Task ValidateAsync_WithEmptyCorrelationId_ShouldFail()
    {
        // Arrange
        var invalidEvent = new CreateProcessEvent
        {
            ProcessId = 1,
            CorrelationId = Guid.Empty,
            Origin = "55.6761,12.5683",
            Destination = "55.6863,12.5700",
            TimeOfTravel = new TimeOnly(10, 30),
            CreatedAt = DateTime.UtcNow,
            ModelVersion = "v1.0"
        };

        // Act
        var result = await _validator.ValidateAsync(invalidEvent, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => 
            e.PropertyName == nameof(CreateProcessEvent.CorrelationId) &&
            e.ErrorMessage == "[Validator] CorrelationId is required");
    }

    [Fact]
    public async Task ValidateAsync_WithValidCorrelationId_ShouldPass()
    {
        // Arrange
        var validEvent = new CreateProcessEvent
        {
            ProcessId = 1,
            CorrelationId = Guid.NewGuid(),
            Origin = "55.6761,12.5683",
            Destination = "55.6863,12.5700",
            TimeOfTravel = new TimeOnly(10, 30),
            CreatedAt = DateTime.UtcNow,
            ModelVersion = "v1.0"
        };

        // Act
        var result = await _validator.ValidateAsync(validEvent, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region Origin Validation

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task ValidateAsync_WithEmptyOrigin_ShouldFail(string emptyOrigin)
    {
        // Arrange
        var invalidEvent = new CreateProcessEvent
        {
            ProcessId = 1,
            CorrelationId = Guid.NewGuid(),
            Origin = emptyOrigin,
            Destination = "55.6863,12.5700",
            TimeOfTravel = new TimeOnly(10, 30),
            CreatedAt = DateTime.UtcNow,
            ModelVersion = "v1.0"
        };

        // Act
        var result = await _validator.ValidateAsync(invalidEvent, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => 
            e.PropertyName == nameof(CreateProcessEvent.Origin) &&
            e.ErrorMessage == "[Validator] Origin must be specified");
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("55.6761")]
    [InlineData(",12.5683")]
    [InlineData("55.6761,")]
    [InlineData("abc,def")]
    [InlineData("55.abc,12.def")]
    [InlineData("lat,lon")]
    [InlineData("55.6761;12.5683")] // Wrong delimiter
    [InlineData("55.6761 12.5683")] // Space instead of comma
    public async Task ValidateAsync_WithInvalidOriginFormat_ShouldFail(string invalidOrigin)
    {
        // Arrange
        var invalidEvent = new CreateProcessEvent
        {
            ProcessId = 1,
            CorrelationId = Guid.NewGuid(),
            Origin = invalidOrigin,
            Destination = "55.6863,12.5700",
            TimeOfTravel = new TimeOnly(10, 30),
            CreatedAt = DateTime.UtcNow,
            ModelVersion = "v1.0"
        };

        // Act
        var result = await _validator.ValidateAsync(invalidEvent, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => 
            e.PropertyName == nameof(CreateProcessEvent.Origin) &&
            e.ErrorMessage == "[Validator] Origin must be in 'lat,lon' format with numeric values");
    }

    #endregion

    #region Destination Validation

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task ValidateAsync_WithEmptyDestination_ShouldFail(string emptyDestination)
    {
        // Arrange
        var invalidEvent = new CreateProcessEvent
        {
            ProcessId = 1,
            CorrelationId = Guid.NewGuid(),
            Origin = "55.6761,12.5683",
            Destination = emptyDestination,
            TimeOfTravel = new TimeOnly(10, 30),
            CreatedAt = DateTime.UtcNow,
            ModelVersion = "v1.0"
        };

        // Act
        var result = await _validator.ValidateAsync(invalidEvent, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => 
            e.PropertyName == nameof(CreateProcessEvent.Destination) &&
            e.ErrorMessage == "[Validator] Destination must be specified");
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("55.6863")]
    [InlineData(",12.5700")]
    [InlineData("55.6863,")]
    [InlineData("xyz,123")]
    [InlineData("55.xyz,12.abc")]
    [InlineData("destination")]
    [InlineData("55.6863;12.5700")] // Wrong delimiter
    [InlineData("55.6863 12.5700")] // Space instead of comma
    public async Task ValidateAsync_WithInvalidDestinationFormat_ShouldFail(string invalidDestination)
    {
        // Arrange
        var invalidEvent = new CreateProcessEvent
        {
            ProcessId = 1,
            CorrelationId = Guid.NewGuid(),
            Origin = "55.6761,12.5683",
            Destination = invalidDestination,
            TimeOfTravel = new TimeOnly(10, 30),
            CreatedAt = DateTime.UtcNow,
            ModelVersion = "v1.0"
        };

        // Act
        var result = await _validator.ValidateAsync(invalidEvent, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => 
            e.PropertyName == nameof(CreateProcessEvent.Destination) &&
            e.ErrorMessage == "[Validator] Destination must be in 'lat,lon' format with numeric values");
    }

    #endregion

    #region Origin vs Destination Validation

    [Theory]
    [InlineData("55.6761,12.5683", "55.6761,12.5683")]
    [InlineData("0.0,0.0", "0.0,0.0")]
    [InlineData("-90.0,-180.0", "-90.0,-180.0")]
    public async Task ValidateAsync_WithSameOriginAndDestination_ShouldFail(string sameCoordinates1, string sameCoordinates2)
    {
        // Arrange
        var invalidEvent = new CreateProcessEvent
        {
            ProcessId = 1,
            CorrelationId = Guid.NewGuid(),
            Origin = sameCoordinates1,
            Destination = sameCoordinates2,
            TimeOfTravel = new TimeOnly(10, 30),
            CreatedAt = DateTime.UtcNow,
            ModelVersion = "v1.0"
        };

        // Act
        var result = await _validator.ValidateAsync(invalidEvent, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => 
            e.PropertyName == nameof(CreateProcessEvent.Origin) &&
            e.ErrorMessage == "[Validator] Origin and Destination must be different");
    }

    [Theory]
    [InlineData("55.6761,12.5683", "55.6863,12.5700")]
    [InlineData("0.0,0.0", "1.0,1.0")]
    [InlineData("-90.0,-180.0", "90.0,180.0")]
    public async Task ValidateAsync_WithDifferentOriginAndDestination_ShouldPass(string origin, string destination)
    {
        // Arrange
        var validEvent = new CreateProcessEvent
        {
            ProcessId = 1,
            CorrelationId = Guid.NewGuid(),
            Origin = origin,
            Destination = destination,
            TimeOfTravel = new TimeOnly(10, 30),
            CreatedAt = DateTime.UtcNow,
            ModelVersion = "v1.0"
        };

        // Act
        var result = await _validator.ValidateAsync(validEvent, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region CreatedAt Validation

    [Fact]
    public async Task ValidateAsync_WithDefaultCreatedAt_ShouldFail()
    {
        // Arrange
        var invalidEvent = new CreateProcessEvent
        {
            ProcessId = 1,
            CorrelationId = Guid.NewGuid(),
            Origin = "55.6761,12.5683",
            Destination = "55.6863,12.5700",
            TimeOfTravel = new TimeOnly(10, 30),
            CreatedAt = default(DateTime), // default is DateTime.MinValue
            ModelVersion = "v1.0"
        };

        // Act
        var result = await _validator.ValidateAsync(invalidEvent, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => 
            e.PropertyName == nameof(CreateProcessEvent.CreatedAt) &&
            e.ErrorMessage == "[Validator] CreatedAt must be set");
    }

    [Theory]
    [InlineData("2025-11-19T10:30:00")]
    [InlineData("2020-01-01T00:00:00")]
    [InlineData("2030-12-31T23:59:59")]
    public async Task ValidateAsync_WithValidCreatedAt_ShouldPass(string dateTimeString)
    {
        // Arrange
        var validEvent = new CreateProcessEvent
        {
            ProcessId = 1,
            CorrelationId = Guid.NewGuid(),
            Origin = "55.6761,12.5683",
            Destination = "55.6863,12.5700",
            TimeOfTravel = new TimeOnly(10, 30),
            CreatedAt = DateTime.Parse(dateTimeString),
            ModelVersion = "v1.0"
        };

        // Act
        var result = await _validator.ValidateAsync(validEvent, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region ModelVersion Validation

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task ValidateAsync_WithEmptyModelVersion_ShouldFail(string emptyModelVersion)
    {
        // Arrange
        var invalidEvent = new CreateProcessEvent
        {
            ProcessId = 1,
            CorrelationId = Guid.NewGuid(),
            Origin = "55.6761,12.5683",
            Destination = "55.6863,12.5700",
            TimeOfTravel = new TimeOnly(10, 30),
            CreatedAt = DateTime.UtcNow,
            ModelVersion = emptyModelVersion
        };

        // Act
        var result = await _validator.ValidateAsync(invalidEvent, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => 
            e.PropertyName == nameof(CreateProcessEvent.ModelVersion) &&
            e.ErrorMessage == "[Validator] ModelVersion must be specified");
    }

    [Theory]
    [InlineData("v1.0")]
    [InlineData("v2.5.3")]
    [InlineData("beta")]
    [InlineData("v999.999.999-alpha-beta-gamma")]
    [InlineData("1")]
    public async Task ValidateAsync_WithValidModelVersion_ShouldPass(string validModelVersion)
    {
        // Arrange
        var validEvent = new CreateProcessEvent
        {
            ProcessId = 1,
            CorrelationId = Guid.NewGuid(),
            Origin = "55.6761,12.5683",
            Destination = "55.6863,12.5700",
            TimeOfTravel = new TimeOnly(10, 30),
            CreatedAt = DateTime.UtcNow,
            ModelVersion = validModelVersion
        };

        // Act
        var result = await _validator.ValidateAsync(validEvent, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region Multiple Validation Failures

    [Fact]
    public async Task ValidateAsync_WithAllInvalidFields_ShouldReturnAllErrors()
    {
        // Arrange
        var completelyInvalidEvent = new CreateProcessEvent
        {
            ProcessId = 0, // Invalid - 1 error
            CorrelationId = Guid.Empty, // Invalid - 1 error
            Origin = "", // Invalid - 2 errors (NotEmpty + Must(BeLatLon))
            Destination = "", // Invalid - 2 errors (NotEmpty + Must(BeLatLon))
            TimeOfTravel = new TimeOnly(10, 30),
            CreatedAt = default(DateTime), // Invalid - 1 error
            ModelVersion = "" // Invalid - 1 error
        };
        // Total: 1 + 1 + 2 + 2 + 1 + 1 + 1 (Origin=Destination) = 9 errors

        // Act
        var result = await _validator.ValidateAsync(completelyInvalidEvent, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeFalse();
        
        // FluentValidation runs ALL validators, so empty strings trigger both NotEmpty AND BeLatLon
        // Origin: NotEmpty + Must(BeLatLon) = 2 errors
        // Destination: NotEmpty + Must(BeLatLon) = 2 errors
        // Plus Origin=Destination check = 1 more error
        // Total: ProcessId(1) + CorrelationId(1) + Origin(2) + Destination(2) + Origin=Destination(1) + CreatedAt(1) + ModelVersion(1) = 9
        result.Errors.Should().HaveCount(9);
        
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateProcessEvent.ProcessId));
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateProcessEvent.CorrelationId));
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateProcessEvent.Origin));
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateProcessEvent.Destination));
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateProcessEvent.CreatedAt));
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateProcessEvent.ModelVersion));
    }

    [Fact]
    public async Task ValidateAsync_WithMultipleCoordinateErrors_ShouldReturnBothErrors()
    {
        // Arrange
        var invalidEvent = new CreateProcessEvent
        {
            ProcessId = 1,
            CorrelationId = Guid.NewGuid(),
            Origin = "invalid", // Invalid format
            Destination = "also-invalid", // Invalid format
            TimeOfTravel = new TimeOnly(10, 30),
            CreatedAt = DateTime.UtcNow,
            ModelVersion = "v1.0"
        };

        // Act
        var result = await _validator.ValidateAsync(invalidEvent, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCountGreaterThanOrEqualTo(2);
        result.Errors.Should().Contain(e => 
            e.PropertyName == nameof(CreateProcessEvent.Origin) &&
            e.ErrorMessage.Contains("lat,lon"));
        result.Errors.Should().Contain(e => 
            e.PropertyName == nameof(CreateProcessEvent.Destination) &&
            e.ErrorMessage.Contains("lat,lon"));
    }

    #endregion

    #region BeLatLon Edge Cases

    [Theory]
    [InlineData("55.6761,,12.5683")] // Double comma (RemoveEmptyEntries handles this)
    [InlineData("55.6761,,,12.5683")] // Triple comma
    [InlineData("  55.6761  ,  12.5683  ")] // Lots of whitespace
    public async Task ValidateAsync_WithMalformedButValidCoordinates_ShouldPass(string coordinates)
    {
        // Arrange
        // RemoveEmptyEntries and TrimEntries make these valid
        var validEvent = new CreateProcessEvent
        {
            ProcessId = 1,
            CorrelationId = Guid.NewGuid(),
            Origin = coordinates,
            Destination = "55.6863,12.5700",
            TimeOfTravel = new TimeOnly(10, 30),
            CreatedAt = DateTime.UtcNow,
            ModelVersion = "v1.0"
        };

        // Act
        var result = await _validator.ValidateAsync(validEvent, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeTrue($"'{coordinates}' should be cleaned by RemoveEmptyEntries and TrimEntries");
    }

    [Theory]
    [InlineData(",,")]
    [InlineData(",")]
    [InlineData("   ,   ")]
    public async Task ValidateAsync_WithOnlyCommasAndSpaces_ShouldFail(string invalidCoordinates)
    {
        // Arrange
        var invalidEvent = new CreateProcessEvent
        {
            ProcessId = 1,
            CorrelationId = Guid.NewGuid(),
            Origin = invalidCoordinates,
            Destination = "55.6863,12.5700",
            TimeOfTravel = new TimeOnly(10, 30),
            CreatedAt = DateTime.UtcNow,
            ModelVersion = "v1.0"
        };

        // Act
        var result = await _validator.ValidateAsync(invalidEvent, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateProcessEvent.Origin));
    }

    #endregion
}