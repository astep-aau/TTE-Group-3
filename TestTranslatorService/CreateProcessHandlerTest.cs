using Microsoft.Extensions.Logging;
using Xunit;
using Moq;
using translator_service.Domain.Entities;
using RouteEstimationService.Domain.Entities.Events;
using translator_service.Features.CreateProcess;
using translator_service.Features.GetRoute;
using MassTransit;

namespace TestTranslatorService;

public class CreateProcessHandlerTest
{
    //Opsætning af dependency injection mocks
    private readonly Mock<ICreateProcessRepository> _mockRepository = new();
    private readonly Mock<ILogger<CreateProcessHandler>> _mockHandlerLogger = new();
    private readonly Mock<CreateProcessEmitter> _mockEmitter = new();

    public CreateProcessHandlerTest()
    {
        _mockRepository = new Mock<ICreateProcessRepository>();
        _mockHandlerLogger = new  Mock<ILogger<CreateProcessHandler>>();
        _mockEmitter = new Mock<CreateProcessEmitter>(new Mock<IBus>().Object);
    }
    
    [Fact]
    public async Task TestOkCreateProcess()
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
        
        var handler = new CreateProcessHandler(
            _mockRepository.Object, 
            _mockHandlerLogger.Object, 
            _mockEmitter.Object);

        // Act
        await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        _mockRepository.Verify(
            repo => repo.CreateProcessAsync(It.Is<CreateProcessRequest>(req => 
                req.Id == command.Id &&
                req.CorrelationId == command.CorrelationId &&
                req.Origin == command.Origin &&
                req.Destination == command.Destination &&
                req.CreatedAt == command.CreatedAt &&
                req.ModelVersion == command.ModelVersion &&
                req.TimeOfTravel == command.TimeOfTravel
                )),
            Times.Once);

        _mockEmitter.Verify(
            repo => repo.EmitCreateProcessEventAsync(It.Is<CreateProcessEvent>(ev =>
                ev.ProcessId == command.Id &&
                ev.CorrelationId == command.CorrelationId &&
                ev.Origin == command.Origin &&
                ev.Destination == command.Destination &&
                ev.TimeOfTravel == command.TimeOfTravel &&
                ev.CreatedAt == command.CreatedAt &&
                ev.ModelVersion == command.ModelVersion
                ), 
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TestBadCreateProcess()
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
        
        var handler = new CreateProcessHandler(
            _mockRepository.Object, 
            _mockHandlerLogger.Object, 
            _mockEmitter.Object);
        
        _mockRepository.Setup(repo => repo.CreateProcessAsync(It.IsAny<CreateProcessRequest>()))
            .ThrowsAsync(new Exception());
        
        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task TestCancelledCreateProcess()
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
        
        var handler = new CreateProcessHandler(
            _mockRepository.Object, 
            _mockHandlerLogger.Object, 
            _mockEmitter.Object);
        
        _mockRepository.Setup(repo => repo.CreateProcessAsync(It.IsAny<CreateProcessRequest>()))
            .ThrowsAsync(new OperationCanceledException());
        
        // Act && Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => handler.HandleAsync(command, CancellationToken.None));
        
        _mockEmitter.Verify(
            emit => emit.EmitCreateProcessEventAsync(It.IsAny<CreateProcessEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
