using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Xunit;
using Moq;
using translator_service.API;
using translator_service.Domain.Entities;
using translator_service.Features.CreateProcess;
using translator_service.Features.GetRoute;

namespace TestTranslatorService;

public class CreateProcessEndpointTest
{
    private readonly Mock<ILogger<CreateProcessEndpoint>> _mockLogger;
    private readonly CreateProcessEndpoint _endpoint;
    private readonly Mock<CreateProcessHandler> _mockHandler;
    
    // Mocks used to create the CreateProcessHandler, not directly used in tests,
    // but needed due to the use of CreateProcessHandler in the endpoint.
    private readonly Mock<ICreateProcessRepository> _mockRepository = new();
    private readonly Mock<ILogger<CreateProcessHandler>> _mockHandlerLogger = new();
    private readonly Mock<CreateProcessEmitter> _mockEmitter = new();

    public CreateProcessEndpointTest()
    {
        // Used for creating the CreateProcessHandler mock
        _mockRepository = new Mock<ICreateProcessRepository>();
        _mockHandlerLogger = new  Mock<ILogger<CreateProcessHandler>>();
        _mockEmitter = new Mock<CreateProcessEmitter>(null);
        
        _mockHandler = new Mock<CreateProcessHandler>(
            _mockRepository.Object,
            _mockHandlerLogger.Object,
            _mockEmitter.Object);
        
        _mockLogger = new Mock<ILogger<CreateProcessEndpoint>>();
        _endpoint = new CreateProcessEndpoint(_mockHandler.Object, _mockLogger.Object);
    }
    
    [Fact]
    public async Task TestOkResponse()
    {
        // Arrange
        var request = new CreateProcessRequest
        {
            Id = 1,
            CorrelationId = Guid.NewGuid(),
            Origin = "123.456789,987.654321",
            Destination = "987.654321,123.456789",
            CreatedAt = DateTime.UtcNow.AddMinutes(-1),
            ModelVersion = "1.0",
            TimeOfTravel = TimeOnly.FromDateTime(DateTime.Now),
            routeCreated = false,
            timeEstimated = false
        };

        _mockHandler.Setup(h => h.HandleAsync(It.IsAny<CreateProcessCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        
        // Act
        var result = await _endpoint.CreateProcessAsync(request, CancellationToken.None);

        // Assert
        Assert.IsType<OkObjectResult>(result);
        _mockHandler.Verify(h => h.HandleAsync(It.IsAny<CreateProcessCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        
    }

    [Fact]
    public async Task TestBadRequestResponse_InvalidRequest()
    {
        // Arrange
        var request = new CreateProcessRequest
        {
            Id = 1,
            CorrelationId = Guid.Empty,
            Origin = "",
            Destination = "987.654321,123.456789",
            CreatedAt = DateTime.UtcNow.AddMinutes(-1),
            ModelVersion = "1.0",
            TimeOfTravel = TimeOnly.FromDateTime(DateTime.Now),
            routeCreated = false,
            timeEstimated = false
        };
        
        // Act
        var result = await _endpoint.CreateProcessAsync(request, CancellationToken.None);
        
        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
        _mockHandler.Verify(h => h.HandleAsync(It.IsAny<CreateProcessCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TestErrorDuringRequestProcess()
    {
        // Test that asserts that if an exception is thrown during the handling of the request, the endpoint returns a 500 status code
        
        // Arrange
        var request = new CreateProcessRequest
        {
            Id = 1,
            CorrelationId = Guid.NewGuid(),
            Origin = "123.456789,987.654321",
            Destination = "987.654321,123.456789",
            CreatedAt = DateTime.UtcNow.AddMinutes(-1),
            ModelVersion = "1.0",
            TimeOfTravel = TimeOnly.FromDateTime(DateTime.Now),
            routeCreated = false,
            timeEstimated = false
        };
        
        _mockHandler.Setup(h => h.HandleAsync(It.IsAny<CreateProcessCommand>(), It.IsAny<CancellationToken>()))
            .Throws<Exception>();
        
        // Act
        var result = await _endpoint.CreateProcessAsync(request, CancellationToken.None);
        
        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task TestCancelledRequestProcess()
    {
        // Arrange
        var request = new CreateProcessRequest
        {
            Id = 1,
            CorrelationId = Guid.NewGuid(),
            Origin = "123.456789,987.654321",
            Destination = "987.654321,123.456789",
            CreatedAt = DateTime.UtcNow.AddMinutes(-1),
            ModelVersion = "1.0",
            TimeOfTravel = TimeOnly.FromDateTime(DateTime.Now),
            routeCreated = false,
            timeEstimated = false
        };
        
        _mockHandler.Setup(h => h.HandleAsync(It.IsAny<CreateProcessCommand>(), It.IsAny<CancellationToken>()))
            .Throws<OperationCanceledException>();
        
        // Act
        var result = await _endpoint.CreateProcessAsync(request, CancellationToken.None);
        
        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(499, statusCodeResult.StatusCode);
    }
}