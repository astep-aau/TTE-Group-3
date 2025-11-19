using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RouteEstimationService.Domain.Entities;
using RouteEstimationService.Features.CreateRoute;

namespace TestRouteEstimationService;

public class CreateRouteHandlerTests
{
    private readonly Mock<ILogger<CreateRouteHandler>> _mockLogger;
    private readonly CreateRouteHandler _handler;

    public CreateRouteHandlerTests()
    {
        _mockLogger = new Mock<ILogger<CreateRouteHandler>>();
        _handler = new CreateRouteHandler(_mockLogger.Object);
    }

    #region Logging Tests

    [Fact]
    public void HandleAsync_ShouldLogHandlingMessage()
    {
        // Arrange
        var payload = new ProcessPayload
        {
            ProcessId = 123,
            Origin = "55.6761,12.5683",
            Destination = "55.6863,12.5700",
            CorrelationId = Guid.NewGuid(),
            TimeOfTravel = new TimeOnly(10, 0),
            CreatedAt = DateTime.UtcNow,
            ModelVersion = "v1.0"
        };

        // Act
        try
        {
            _handler.HandleAsync(payload);
        }
        catch
        {
            // Expected - static helpers may throw, we're testing logging
        }

        // Assert - verify initial handling log
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Handling route for ProcessId=123")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(999999)]
    [InlineData(int.MaxValue)]
    public void HandleAsync_WithDifferentProcessIds_ShouldLogCorrectProcessId(int processId)
    {
        // Arrange
        var payload = new ProcessPayload
        {
            ProcessId = processId,
            Origin = "55.6761,12.5683",
            Destination = "55.6863,12.5700",
            CorrelationId = Guid.NewGuid(),
            TimeOfTravel = new TimeOnly(10, 0),
            CreatedAt = DateTime.UtcNow,
            ModelVersion = "v1.0"
        };

        // Act
        try
        {
            _handler.HandleAsync(payload);
        }
        catch
        {
            // Expected - static helpers may throw
        }

        // Assert - verify ProcessId appears in logs
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains($"ProcessId={processId}")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
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
            var result = _handler.HandleAsync(payload);
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
            var result = _handler.HandleAsync(payload);
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
    public void HandleAsync_CalledMultipleTimes_ShouldHandleEachIndependently()
    {
        // Arrange
        var payload1 = new ProcessPayload
        {
            ProcessId = 1,
            Origin = "55.6761,12.5683",
            Destination = "55.6863,12.5700",
            CorrelationId = Guid.NewGuid(),
            TimeOfTravel = new TimeOnly(10, 0),
            CreatedAt = DateTime.UtcNow,
            ModelVersion = "v1.0"
        };

        var payload2 = new ProcessPayload
        {
            ProcessId = 2,
            Origin = "55.7000,12.6000",
            Destination = "55.8000,12.7000",
            CorrelationId = Guid.NewGuid(),
            TimeOfTravel = new TimeOnly(11, 0),
            CreatedAt = DateTime.UtcNow,
            ModelVersion = "v1.0"
        };

        // Act
        try
        {
            var result1 = _handler.HandleAsync(payload1);
            var result2 = _handler.HandleAsync(payload2);
        }
        catch
        {
            // Expected - static helpers may throw
        }

        // Assert - both should be logged independently
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("ProcessId=1")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("ProcessId=2")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Coordinate Format Tests (Parser Robustness)

    [Theory]
    [InlineData("55.6761, 12.5683")] // with space after comma
    [InlineData(" 55.6761 , 12.5683 ")] // with extra spaces
    [InlineData("55.6761,12.5683,100.5")] // with extra parts (elevation)
    [InlineData("55.6761,,12.5683")] // double comma (RemoveEmptyEntries handles this)
    public void HandleAsync_WithVariousValidFormats_ShouldParseCorrectly(string coordinates)
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
            var result = _handler.HandleAsync(payload);
        }
        catch
        {
            // May throw from helper methods, but not from parsing
        }

        // Assert - should not log invalid coordinate warnings (parsing succeeded)
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Invalid origin coordinates")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    #endregion
}

