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
    private readonly Mock<IValidator<CreateProcessEvent>> _mockValidator;
    private readonly Mock<ConsumeContext<CreateProcessEvent>> _mockContext;
    private readonly CreateRouteConsumer _consumer;

    public CreateRouteConsumerTests()
    {
        _mockHandler = new Mock<ICreateRouteHandler>();
        var mockLogger = new Mock<ILogger<CreateRouteConsumer>>();
        _mockValidator = new Mock<IValidator<CreateProcessEvent>>();
        _mockContext = new Mock<ConsumeContext<CreateProcessEvent>>();

        _consumer = new CreateRouteConsumer(
            _mockHandler.Object,
            mockLogger.Object,
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
    public async Task Consume_WithInvalidEvent_ShouldNotCallHandler()
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
        _mockValidator.Verify(v => v.ValidateAsync(createProcessEvent, CancellationToken.None), Times.Once);
        _mockHandler.Verify(h => h.HandleAsync(It.IsAny<ProcessPayload>()), Times.Never);
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

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _consumer.Consume(_mockContext.Object));
        
        Assert.Equal("Handler processing failed", exception.Message);
        _mockHandler.Verify(h => h.HandleAsync(It.IsAny<ProcessPayload>()), Times.Once);
    }

    [Fact]
    public async Task Consume_WhenHandlerFails_ValidationStillRuns()
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

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _consumer.Consume(_mockContext.Object));
        
        _mockValidator.Verify(v => v.ValidateAsync(createProcessEvent, CancellationToken.None), Times.Once);
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
        Assert.NotNull(capturedPayload);
        Assert.Equal(processId, capturedPayload.ProcessId);
        Assert.Equal(correlationId, capturedPayload.CorrelationId);
        Assert.Equal(origin, capturedPayload.Origin);
        Assert.Equal(destination, capturedPayload.Destination);
        Assert.Equal(timeOfTravel, capturedPayload.TimeOfTravel);
        Assert.Equal(createdAt, capturedPayload.CreatedAt);
        Assert.Equal(modelVersion, capturedPayload.ModelVersion);
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