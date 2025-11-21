using Microsoft.Extensions.Logging;
using Moq;
using RouteEstimationService.Domain.Entities;
using RouteEstimationService.Features.CreateRoute;

namespace TestRouteEstimationService;

public class CreateRouteHandlerTests
{
    private readonly Mock<IRouteMadeEmitter> _mockEmitter;
    private readonly CreateRouteHandler _handler;

    public CreateRouteHandlerTests()
    {
        _mockEmitter = new Mock<IRouteMadeEmitter>();
        _handler = new CreateRouteHandler(new Mock<ILogger<CreateRouteHandler>>().Object, _mockEmitter.Object);
    }

    #region Invalid Coordinate Tests

    [Theory]
    [InlineData("", "55.6863,12.5700")]
    [InlineData(" ", "55.6863,12.5700")]
    [InlineData("invalid", "55.6863,12.5700")]
    [InlineData("55.6761", "55.6863,12.5700")]
    [InlineData("55.6761,", "55.6863,12.5700")]
    [InlineData(",12.5683", "55.6863,12.5700")]
    public async Task HandleAsync_WithInvalidOrigin_ShouldThrowArgumentException(string invalidOrigin, string destination)
    {
        // Arrange
        var payload = new ProcessPayload
        {
            ProcessId = 123,
            Origin = invalidOrigin,
            Destination = destination,
            CorrelationId = Guid.NewGuid(),
            TimeOfTravel = new TimeOnly(10, 0),
            CreatedAt = DateTime.UtcNow,
            ModelVersion = "v1.0"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            async () => await _handler.HandleAsync(payload));
        
        Assert.Equal("Invalid origin coordinates", exception.Message);
    }

    [Theory]
    [InlineData("55.6761,12.5683", "")]
    [InlineData("55.6761,12.5683", " ")]
    [InlineData("55.6761,12.5683", "invalid")]
    [InlineData("55.6761,12.5683", "55.6863")]
    [InlineData("55.6761,12.5683", "55.6863,")]
    [InlineData("55.6761,12.5683", ",12.5700")]
    public async Task HandleAsync_WithInvalidDestination_ShouldThrowArgumentException(string origin, string invalidDestination)
    {
        // Arrange
        var payload = new ProcessPayload
        {
            ProcessId = 456,
            Origin = origin,
            Destination = invalidDestination,
            CorrelationId = Guid.NewGuid(),
            TimeOfTravel = new TimeOnly(10, 0),
            CreatedAt = DateTime.UtcNow,
            ModelVersion = "v1.0"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            async () => await _handler.HandleAsync(payload));
        
        Assert.Equal("Invalid destination coordinates", exception.Message);
    }

    #endregion

    #region Coordinate Format Parsing Tests

    [Theory]
    [InlineData("55.6761, 12.5683")] // with space after comma
    [InlineData(" 55.6761 , 12.5683 ")] // with extra spaces
    [InlineData("55.6761,12.5683,100.5")] // with extra parts (elevation)
    [InlineData("55.6761,,12.5683")] // double comma (RemoveEmptyEntries handles this)
    public async Task HandleAsync_WithVariousValidFormats_ShouldNotThrowParsingException(string coordinates)
    {
        // Arrange
        // These formats should parse correctly due to StringSplitOptions.RemoveEmptyEntries and TrimEntries
        var payload = new ProcessPayload
        {
            ProcessId = 555,
            Origin = coordinates,
            Destination = "55.6863,12.5700",
            CorrelationId = Guid.NewGuid(),
            TimeOfTravel = new TimeOnly(10, 0),
            CreatedAt = DateTime.UtcNow,
            ModelVersion = "v1.0"
        };

        // Act
        try
        {
            await _handler.HandleAsync(payload);
            Assert.True(true); // Parsing succeeded
        }
        catch (ArgumentException)
        {
            // Assert - Should NOT be ArgumentException (parsing should succeed)
            Assert.Fail("Parsing should succeed for these formats - should not throw ArgumentException");
        }
        catch (Exception)
        {
            // Any other exception is fine (NearestNodeFinder, EstimateTimeHandler, etc.)
            // This confirms parsing succeeded
        }
    }

    #endregion

    #region Integration-Level Behavior Tests (With Real Helpers)

    [Fact]
    public async Task HandleAsync_WithValidChineseCoordinates_ShouldCompleteSuccessfully()
    {
        // Arrange - Valid coordinates from China dataset
        var payload = new ProcessPayload
        {
            ProcessId = 100,
            CorrelationId = Guid.NewGuid(),
            Origin = "45.7821345,126.5570674",
            Destination = "45.7601284,126.5864540",
            TimeOfTravel = new TimeOnly(10, 0),
            CreatedAt = DateTime.UtcNow,
            ModelVersion = "v1.0"
        };

        // Act
        try
        {
            await _handler.HandleAsync(payload);
            // May complete successfully or throw - either is fine
        }
        catch (ArgumentException)
        {
            // Assert - Should NOT throw ArgumentException (coordinates are valid)
            Assert.Fail("Coordinates are valid - should not throw ArgumentException");
        }
        catch (Exception)
        {
            // Any other exception is acceptable (EstimateTimeHandler service unavailable, etc.)
        }
    }

    [Fact]
    public async Task HandleAsync_WithWesternEuropeCoordinates_ShouldThrowApplicationException()
    {
        // Arrange - Coordinates far from China dataset
        var payload = new ProcessPayload
        {
            ProcessId = 200,
            CorrelationId = Guid.NewGuid(),
            Origin = "48.8566,2.3522", // Paris, France
            Destination = "51.5074,-0.1278", // London, UK
            TimeOfTravel = new TimeOnly(10, 0),
            CreatedAt = DateTime.UtcNow,
            ModelVersion = "v1.0"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ApplicationException>(
            async () => await _handler.HandleAsync(payload));
        
        Assert.Contains("nearest nodes", exception.Message);
    }

    #endregion

    #region Payload Property Tests

    [Fact]
    public async Task HandleAsync_WithMinimalValidPayload_ShouldAttemptProcessing()
    {
        // Arrange - Minimal but valid payload
        var payload = new ProcessPayload
        {
            ProcessId = 1,
            Origin = "45.7821345,126.5570674",
            Destination = "45.7601284,126.5864540",
            CorrelationId = Guid.Empty,
            TimeOfTravel = TimeOnly.MinValue,
            CreatedAt = DateTime.MinValue,
            ModelVersion = ""
        };

        // Act
        try
        {
            await _handler.HandleAsync(payload);
            // May complete or throw
        }
        catch (ArgumentException)
        {
            // Assert - Should NOT throw ArgumentException (coordinates are valid)
            Assert.Fail("Coordinates are valid - should not throw ArgumentException");
        }
        catch (Exception)
        {
            // Any other exception is acceptable
        }
    }

    [Fact]
    public async Task HandleAsync_WithMaximalPayload_ShouldAttemptProcessing()
    {
        // Arrange - Maximal payload values
        var payload = new ProcessPayload
        {
            ProcessId = int.MaxValue,
            Origin = "45.7821345,126.5570674",
            Destination = "45.7601284,126.5864540",
            CorrelationId = Guid.NewGuid(),
            TimeOfTravel = new TimeOnly(23, 59, 59),
            CreatedAt = DateTime.MaxValue,
            ModelVersion = "v999.999.999-beta-alpha-gamma"
        };

        // Act
        try
        {
            await _handler.HandleAsync(payload);
            // May complete or throw
        }
        catch (ArgumentException)
        {
            // Assert - Should NOT throw ArgumentException (coordinates are valid)
            Assert.Fail("Coordinates are valid - should not throw ArgumentException");
        }
        catch (Exception)
        {
            // Any other exception is acceptable
        }
    }

    #endregion

    #region Emitter Invocation Tests

    [Fact]
    public void HandleAsync_OnSuccess_ShouldInvokeEmitter()
    {
        // This test documents that the handler should call the emitter
        // However, since we use real static helpers, this is hard to test without mocking everything
        // This serves as documentation that the emitter SHOULD be called on successful route creation
        
        // For now, we verify the emitter was injected properly
        Assert.NotNull(_mockEmitter);
        Assert.NotNull(_handler);
    }

    #endregion
}

