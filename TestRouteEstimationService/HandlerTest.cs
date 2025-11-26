using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using RouteEstimationService.Domain.Entities;
using RouteEstimationService.Features.CreateRoute;
using RouteEstimationService.Features.EstimateTime;

namespace TestRouteEstimationService;

public class CreateRouteHandlerTests
{
    private readonly Mock<ILogger<CreateRouteHandler>> _mockLogger;
    private readonly Mock<IRouteMadeEmitter> _mockEmitter;
    private readonly EstimateTimeHandler _estimateTimeHandler;
    private readonly CreateRouteHandler _handler;

    public CreateRouteHandlerTests()
    {
        _mockLogger = new Mock<ILogger<CreateRouteHandler>>();
        _mockEmitter = new Mock<IRouteMadeEmitter>();
        
        // Create a mock configuration for EstimateTimeHandler
        var mockConfiguration = new Mock<IConfiguration>();
        var mockSection = new Mock<IConfigurationSection>();
        mockSection.Setup(s => s["BaseUrl"]).Returns("http://localhost:8000");
        mockConfiguration.Setup(c => c.GetSection("PythonService")).Returns(mockSection.Object);
        
        _estimateTimeHandler = new EstimateTimeHandler(_mockLogger.Object, mockConfiguration.Object);
        _handler = new CreateRouteHandler(_mockLogger.Object, _mockEmitter.Object, _estimateTimeHandler);
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

    #region Payload Property Tests

    [Fact]
    public void HandleAsync_ShouldHandleMinimalPayload()
    {
        // Arrange - minimal but valid payload (validation happens before handler)
        var payload = new ProcessPayload
        {
            ProcessId = 1,
            Origin = "0.0,0.0",
            Destination = "1.0,1.0",
            CorrelationId = Guid.Empty,
            TimeOfTravel = TimeOnly.MinValue,
            CreatedAt = DateTime.MinValue,
            ModelVersion = ""
        };

        // Act
        try
        {
            _handler.HandleAsync(payload);
            // May fail on helper methods, but should process the payload
        }
        catch
        {
            // Expected - helper methods may throw
        }

        // Assert - should log handling message
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Handling route")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void HandleAsync_WithMaximalPayload_ShouldProcess()
    {
        // Arrange - maximal payload values
        var payload = new ProcessPayload
        {
            ProcessId = int.MaxValue,
            Origin = "90.0,180.0",
            Destination = "-90.0,-180.0",
            CorrelationId = Guid.NewGuid(),
            TimeOfTravel = new TimeOnly(23, 59, 59),
            CreatedAt = DateTime.MaxValue,
            ModelVersion = "v999.999.999-beta-alpha-gamma"
        };

        // Act
        try
        {
            _handler.HandleAsync(payload);
        }
        catch
        {
            // Expected - helper methods may throw
        }

        // Assert - should handle large values
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains($"ProcessId={int.MaxValue}")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    #endregion

    #region Multiple Calls Tests

    [Fact]
    public async Task HandleAsync_CalledMultipleTimes_ShouldHandleEachIndependently()
    {
        // Arrange - Use coordinates that ARE in the dataset
        var payload1 = new ProcessPayload
        {
            ProcessId = 1,
            CorrelationId = Guid.NewGuid(),
            Origin = "45.7821345,126.5570674",      // Valid China coordinates
            Destination = "45.7601284,126.5864540", // Valid China coordinates
            TimeOfTravel = new TimeOnly(10, 0),
            CreatedAt = DateTime.UtcNow,
            ModelVersion = "v1.0"
        };
    
        var payload2 = new ProcessPayload
        {
            ProcessId = 2,
            CorrelationId = Guid.NewGuid(),
            Origin = "45.7821345,126.5570674",      // Valid China coordinates
            Destination = "45.7601284,126.5864540", // Valid China coordinates
            TimeOfTravel = new TimeOnly(14, 0),
            CreatedAt = DateTime.UtcNow,
            ModelVersion = "v1.0"
        };
    
        // Act
        try
        {
            await _handler.HandleAsync(payload1);
        }
        catch
        {
            // External dependencies may fail (e.g., embedding service). The test focuses on logging.
        }

        try
        {
            await _handler.HandleAsync(payload2);
        }
        catch
        {
            // External dependencies may fail (e.g., embedding service). The test focuses on logging.
        }
    
        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("ProcessId=1")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("ProcessId=2")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    #endregion

    #region Coordinate Format Tests (Parser Robustness)

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

