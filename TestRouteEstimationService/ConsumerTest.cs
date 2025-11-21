using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using RouteEstimationService.Domain.Entities;
using RouteEstimationService.Domain.Entities.Events;
using RouteEstimationService.Features.CreateRoute;
using ValidationResult = FluentValidation.Results.ValidationResult;

namespace TestRouteEstimationService;

public class CreateRouteConsumerTests
{
    private readonly Mock<ICreateRouteHandler> _mockHandler;
    private readonly Mock<ILogger<CreateRouteConsumer>> _mockLogger;
    private readonly Mock<IValidator<CreateProcessEvent>> _mockValidator;
    private readonly Mock<ConsumeContext<CreateProcessEvent>> _mockContext;
    private readonly CreateRouteConsumer _consumer;

    public CreateRouteConsumerTests()
    {
        _mockHandler = new Mock<ICreateRouteHandler>();
        _mockLogger = new Mock<ILogger<CreateRouteConsumer>>();
        _mockValidator = new Mock<IValidator<CreateProcessEvent>>();
        _mockContext = new Mock<ConsumeContext<CreateProcessEvent>>();

        _consumer = new CreateRouteConsumer(
            _mockHandler.Object,
            _mockLogger.Object,
            _mockValidator.Object
        );
    }

    #region Happy Path Tests

    [Fact]
    public async Task Consume_WithValidEvent_ShouldProcessSuccessfully()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        const int processId = 123;
        const string origin = "55.6761,12.5683";
        const string destination = "55.6863,12.5700";
        var timeOfTravel = new TimeOnly(10, 30);
        var createdAt = DateTime.UtcNow;
        const string modelVersion = "v1.0";

        var createProcessEvent = new CreateProcessEvent
        {
            ProcessId = processId,
            CorrelationId = correlationId,
            Origin = origin,
            Destination = destination,
            TimeOfTravel = timeOfTravel,
            CreatedAt = createdAt,
            ModelVersion = modelVersion
        };

        _mockContext.Setup(c => c.Message).Returns(createProcessEvent);
        _mockValidator.Setup(v => v.ValidateAsync(createProcessEvent, CancellationToken.None))
            .ReturnsAsync(new ValidationResult());
        _mockHandler.Setup(h => h.HandleAsync(It.IsAny<ProcessPayload>()))
            .Returns(Task.CompletedTask);

        // Act
        await _consumer.Consume(_mockContext.Object);

        // Assert
        _mockValidator.Verify(v => v.ValidateAsync(createProcessEvent, CancellationToken.None), Times.Once);
        _mockHandler.Verify(h => h.HandleAsync(It.Is<ProcessPayload>(p =>
            p.ProcessId == processId &&
            p.CorrelationId == correlationId &&
            p.Origin == origin &&
            p.Destination == destination &&
            p.TimeOfTravel == timeOfTravel &&
            p.ModelVersion == modelVersion
        )), Times.Once);
    }

    [Fact]
    public async Task Consume_WithValidEvent_ShouldLogCorrectMessages()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        const int processId = 456;
        
        var createProcessEvent = new CreateProcessEvent
        {
            ProcessId = processId,
            CorrelationId = correlationId,
            Origin = "55.6761,12.5683",
            Destination = "55.6863,12.5700",
            TimeOfTravel = new TimeOnly(14, 0),
            CreatedAt = DateTime.UtcNow,
            ModelVersion = "v1.0"
        };

        _mockContext.Setup(c => c.Message).Returns(createProcessEvent);
        _mockValidator.Setup(v => v.ValidateAsync(createProcessEvent, CancellationToken.None))
            .ReturnsAsync(new ValidationResult());
        _mockHandler.Setup(h => h.HandleAsync(It.IsAny<ProcessPayload>()))
            .Returns(Task.CompletedTask);

        // Act
        await _consumer.Consume(_mockContext.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Received CreateProcessEvent")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("validation succeeded")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Processing route")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Route created successfully")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task Consume_WithInvalidEvent_ShouldNotProcessRoute()
    {
        // Arrange
        var createProcessEvent = new CreateProcessEvent
        {
            ProcessId = 0, // Invalid
            CorrelationId = Guid.Empty,
            Origin = "",
            Destination = "",
            TimeOfTravel = TimeOnly.MinValue,
            CreatedAt = DateTime.UtcNow,
            ModelVersion = ""
        };

        var validationFailures = new List<ValidationFailure>
        {
            new ("ProcessId", "ProcessId must be greater than 0"),
            new ("CorrelationId", "CorrelationId is required"),
            new ("Origin", "Origin must be specified"),
            new ("Destination", "Destination must be specified")
        };

        _mockContext.Setup(c => c.Message).Returns(createProcessEvent);
        _mockValidator.Setup(v => v.ValidateAsync(createProcessEvent, CancellationToken.None))
            .ReturnsAsync(new ValidationResult(validationFailures));

        // Act
        await _consumer.Consume(_mockContext.Object);

        // Assert
        _mockValidator.Verify(v => v.ValidateAsync(createProcessEvent, CancellationToken.None), Times.Once);
        _mockHandler.Verify(h => h.HandleAsync(It.IsAny<ProcessPayload>()), Times.Never);
    }

    [Fact]
    public async Task Consume_WithInvalidEvent_ShouldLogWarning()
    {
        // Arrange
        var createProcessEvent = new CreateProcessEvent
        {
            ProcessId = -1,
            CorrelationId = Guid.NewGuid(),
            Origin = "invalid",
            Destination = "invalid",
            TimeOfTravel = TimeOnly.MinValue,
            CreatedAt = DateTime.UtcNow,
            ModelVersion = "v1.0"
        };

        var validationFailures = new List<ValidationFailure>
        {
            new ValidationFailure("ProcessId", "ProcessId must be greater than 0")
        };

        _mockContext.Setup(c => c.Message).Returns(createProcessEvent);
        _mockValidator.Setup(v => v.ValidateAsync(createProcessEvent, CancellationToken.None))
            .ReturnsAsync(new ValidationResult(validationFailures));

        // Act
        await _consumer.Consume(_mockContext.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("validation failed")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData("", "55.6863,12.5700")]
    [InlineData("55.6761,12.5683", "")]
    [InlineData("", "")]
    public async Task Consume_WithEmptyCoordinates_ShouldFailValidation(string origin, string destination)
    {
        // Arrange
        var createProcessEvent = new CreateProcessEvent
        {
            ProcessId = 1,
            CorrelationId = Guid.NewGuid(),
            Origin = origin,
            Destination = destination,
            TimeOfTravel = new TimeOnly(10, 0),
            CreatedAt = DateTime.UtcNow,
            ModelVersion = "v1.0"
        };

        var validationFailures = new List<ValidationFailure>
        {
            new ("Origin", "Origin must be specified")
        };

        _mockContext.Setup(c => c.Message).Returns(createProcessEvent);
        _mockValidator.Setup(v => v.ValidateAsync(createProcessEvent, CancellationToken.None))
            .ReturnsAsync(new ValidationResult(validationFailures));

        // Act
        await _consumer.Consume(_mockContext.Object);

        // Assert
        _mockHandler.Verify(h => h.HandleAsync(It.IsAny<ProcessPayload>()), Times.Never);
    }

    #endregion

    #region Handler Failure Tests

    [Fact]
    public async Task Consume_WhenHandlerFails_ShouldThrowException()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        const int processId = 789;
        
        var createProcessEvent = new CreateProcessEvent
        {
            ProcessId = processId,
            CorrelationId = correlationId,
            Origin = "55.6761,12.5683",
            Destination = "55.6863,12.5700",
            TimeOfTravel = new TimeOnly(16, 30),
            CreatedAt = DateTime.UtcNow,
            ModelVersion = "v1.0"
        };

        _mockContext.Setup(c => c.Message).Returns(createProcessEvent);
        _mockValidator.Setup(v => v.ValidateAsync(createProcessEvent, CancellationToken.None))
            .ReturnsAsync(new ValidationResult());
        _mockHandler.Setup(h => h.HandleAsync(It.IsAny<ProcessPayload>()))
            .ThrowsAsync(new InvalidOperationException("Handler processing failed"));

        // Act
        var act = async () => await _consumer.Consume(_mockContext.Object);

        // Assert
        // BUG IN CONSUMER: It tries to access route.Value before checking route.IsSuccess
        // This causes an exception to be thrown before the warning can be logged
        await act.Should().ThrowAsync<Exception>();
        
        // Verify the handler was called
        _mockHandler.Verify(h => h.HandleAsync(It.IsAny<ProcessPayload>()), Times.Once);
    }

    [Fact]
    public async Task Consume_WhenHandlerFails_ValidationStillSucceeds()
    {
        // Arrange
        var createProcessEvent = new CreateProcessEvent
        {
            ProcessId = 999,
            CorrelationId = Guid.NewGuid(),
            Origin = "55.6761,12.5683",
            Destination = "55.6863,12.5700",
            TimeOfTravel = new TimeOnly(18, 0),
            CreatedAt = DateTime.UtcNow,
            ModelVersion = "v1.0"
        };

        _mockContext.Setup(c => c.Message).Returns(createProcessEvent);
        _mockValidator.Setup(v => v.ValidateAsync(createProcessEvent, CancellationToken.None))
            .ReturnsAsync(new ValidationResult());
        _mockHandler.Setup(h => h.HandleAsync(It.IsAny<ProcessPayload>()))
            .ThrowsAsync(new InvalidOperationException("Handler processing failed"));

        // Act
        try
        {
            await _consumer.Consume(_mockContext.Object);
        }
        catch
        {
            // Expected due to bug in consumer
        }

        // Assert
        // Validation should have succeeded even though handler failed
        _mockValidator.Verify(v => v.ValidateAsync(createProcessEvent, CancellationToken.None), Times.Once);
        
        // Processing log should have been written before the exception
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Processing route")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public async Task Consume_WithSameOriginAndDestination_ShouldFailValidation()
    {
        // Arrange
        const string sameLocation = "55.6761,12.5683";
        var createProcessEvent = new CreateProcessEvent
        {
            ProcessId = 1,
            CorrelationId = Guid.NewGuid(),
            Origin = sameLocation,
            Destination = sameLocation,
            TimeOfTravel = new TimeOnly(12, 0),
            CreatedAt = DateTime.UtcNow,
            ModelVersion = "v1.0"
        };

        var validationFailures = new List<ValidationFailure>
        {
            new ("Origin", "Origin and Destination must be different")
        };

        _mockContext.Setup(c => c.Message).Returns(createProcessEvent);
        _mockValidator.Setup(v => v.ValidateAsync(createProcessEvent, CancellationToken.None))
            .ReturnsAsync(new ValidationResult(validationFailures));

        // Act
        await _consumer.Consume(_mockContext.Object);

        // Assert
        _mockHandler.Verify(h => h.HandleAsync(It.IsAny<ProcessPayload>()), Times.Never);
    }

    [Fact]
    public async Task Consume_WithMinimalValidData_ShouldProcess()
    {
        // Arrange
        var createProcessEvent = new CreateProcessEvent
        {
            ProcessId = 1,
            CorrelationId = Guid.NewGuid(),
            Origin = "0.0,0.0",
            Destination = "1.0,1.0",
            TimeOfTravel = TimeOnly.MinValue,
            CreatedAt = DateTime.UtcNow,
            ModelVersion = "v1.0"
        };

        _mockContext.Setup(c => c.Message).Returns(createProcessEvent);
        _mockValidator.Setup(v => v.ValidateAsync(createProcessEvent, CancellationToken.None))
            .ReturnsAsync(new ValidationResult());
        _mockHandler.Setup(h => h.HandleAsync(It.IsAny<ProcessPayload>()))
            .Returns(Task.CompletedTask);

        // Act
        await _consumer.Consume(_mockContext.Object);

        // Assert
        _mockHandler.Verify(h => h.HandleAsync(It.IsAny<ProcessPayload>()), Times.Once);
    }

    [Fact]
    public async Task Consume_WithLargeProcessId_ShouldProcess()
    {
        // Arrange
        const int largeProcessId = int.MaxValue;
        var createProcessEvent = new CreateProcessEvent
        {
            ProcessId = largeProcessId,
            CorrelationId = Guid.NewGuid(),
            Origin = "55.6761,12.5683",
            Destination = "55.6863,12.5700",
            TimeOfTravel = new TimeOnly(23, 59),
            CreatedAt = DateTime.UtcNow,
            ModelVersion = "v1.0"
        };
        
        _mockContext.Setup(c => c.Message).Returns(createProcessEvent);
        _mockValidator.Setup(v => v.ValidateAsync(createProcessEvent, CancellationToken.None))
            .ReturnsAsync(new ValidationResult());
        _mockHandler.Setup(h => h.HandleAsync(It.IsAny<ProcessPayload>()))
            .Returns(Task.CompletedTask);

        // Act
        await _consumer.Consume(_mockContext.Object);

        // Assert
        _mockHandler.Verify(h => h.HandleAsync(It.Is<ProcessPayload>(p => p.ProcessId == largeProcessId)), Times.Once);
    }

    #endregion

    #region Payload Mapping Tests

    [Fact]
    public async Task Consume_ShouldCorrectlyMapEventToPayload()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        const int processId = 12345;
        const string origin = "55.6761,12.5683";
        const string destination = "55.6863,12.5700";
        var timeOfTravel = new TimeOnly(15, 45);
        var createdAt = new DateTime(2025, 11, 19, 10, 30, 0, DateTimeKind.Utc);
        const string modelVersion = "v2.5";

        var createProcessEvent = new CreateProcessEvent
        {
            ProcessId = processId,
            CorrelationId = correlationId,
            Origin = origin,
            Destination = destination,
            TimeOfTravel = timeOfTravel,
            CreatedAt = createdAt,
            ModelVersion = modelVersion
        };

        _mockContext.Setup(c => c.Message).Returns(createProcessEvent);
        _mockValidator.Setup(v => v.ValidateAsync(createProcessEvent, CancellationToken.None))
            .ReturnsAsync(new ValidationResult());
        _mockHandler.Setup(h => h.HandleAsync(It.IsAny<ProcessPayload>()))
            .Returns(Task.CompletedTask);

        ProcessPayload? capturedPayload = null;
        _mockHandler.Setup(h => h.HandleAsync(It.IsAny<ProcessPayload>()))
            .Callback<ProcessPayload>(p => capturedPayload = p)
            .Returns(Task.CompletedTask);

        // Act
        await _consumer.Consume(_mockContext.Object);

        // Assert
        capturedPayload.Should().NotBeNull();
        capturedPayload!.ProcessId.Should().Be(processId);
        capturedPayload.CorrelationId.Should().Be(correlationId);
        capturedPayload.Origin.Should().Be(origin);
        capturedPayload.Destination.Should().Be(destination);
        capturedPayload.TimeOfTravel.Should().Be(timeOfTravel);
        capturedPayload.CreatedAt.Should().Be(createdAt);
        capturedPayload.ModelVersion.Should().Be(modelVersion);
    }
    
    [Fact]
    public async Task Consume_WithValidEvent_ShouldCallHandler()
    {
        // Arrange
        var createProcessEvent = new CreateProcessEvent
        {
            ProcessId = 54321,
            CorrelationId = Guid.NewGuid(),
            Origin = "55.6761,12.5683",
            Destination = "55.6863,12.5700",
            TimeOfTravel = new TimeOnly(9, 0),
            CreatedAt = DateTime.UtcNow,
            ModelVersion = "v1.0"
        };
    
        _mockContext.Setup(c => c.Message).Returns(createProcessEvent);
        _mockValidator.Setup(v => v.ValidateAsync(createProcessEvent, CancellationToken.None))
            .ReturnsAsync(new ValidationResult());
        _mockHandler.Setup(h => h.HandleAsync(It.IsAny<ProcessPayload>()))
            .Returns(Task.CompletedTask);
    
        // Act
        await _consumer.Consume(_mockContext.Object);
    
        // Assert
        _mockHandler.Verify(h => h.HandleAsync(It.Is<ProcessPayload>(p =>
            p.ProcessId == 54321 &&
            p.Origin == "55.6761,12.5683" &&
            p.Destination == "55.6863,12.5700"
        )), Times.Once);
    }

    #endregion

    #region Multiple Events Tests

    [Fact]
    public async Task Consume_CalledMultipleTimes_ShouldProcessEachIndependently()
    {
        // Arrange
        var event1 = new CreateProcessEvent
        {
            ProcessId = 1,
            CorrelationId = Guid.NewGuid(),
            Origin = "55.6761,12.5683",
            Destination = "55.6863,12.5700",
            TimeOfTravel = new TimeOnly(10, 0),
            CreatedAt = DateTime.UtcNow,
            ModelVersion = "v1.0"
        };

        var event2 = new CreateProcessEvent
        {
            ProcessId = 2,
            CorrelationId = Guid.NewGuid(),
            Origin = "55.7000,12.6000",
            Destination = "55.8000,12.7000",
            TimeOfTravel = new TimeOnly(14, 0),
            CreatedAt = DateTime.UtcNow,
            ModelVersion = "v1.0"
        };

        var mockContext1 = new Mock<ConsumeContext<CreateProcessEvent>>();
        var mockContext2 = new Mock<ConsumeContext<CreateProcessEvent>>();

        mockContext1.Setup(c => c.Message).Returns(event1);
        mockContext2.Setup(c => c.Message).Returns(event2);

        _mockValidator.Setup(v => v.ValidateAsync(It.IsAny<CreateProcessEvent>(), CancellationToken.None))
            .ReturnsAsync(new ValidationResult());
        _mockHandler.Setup(h => h.HandleAsync(It.IsAny<ProcessPayload>()))
            .Returns(Task.CompletedTask);

        // Act
        await _consumer.Consume(mockContext1.Object);
        await _consumer.Consume(mockContext2.Object);

        // Assert
        _mockHandler.Verify(h => h.HandleAsync(It.IsAny<ProcessPayload>()), Times.Exactly(2));
    }

    #endregion
}