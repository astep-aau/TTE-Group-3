using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using StateService.Domain.Value;
using StateService.Features.EstimationRequested;
using StateService.Infrastructure.Services;
using Xunit;

namespace StateService.Tests;

public class EstimationRequestedHandlerTests
{
    private readonly Mock<IStateMachineService> _stateMachine = new();
    private readonly Mock<ILogger<EstimationRequestedHandler>> _logger = new();

    private EstimationRequestedHandler CreateHandler() =>
        new(_stateMachine.Object, _logger.Object);

    private static EstimationRequestedMessage CreateMessage(int pid = 0, string? start = "A", string? end = "B") =>
        new(pid,
            start ?? string.Empty,
            end ?? string.Empty,
            DateTime.UtcNow.AddHours(1),
            "corr-123");

    [Fact]
    public async Task HandleAsync_WithMissingLocation_ShouldNotInteractWithStateMachine()
    {
        var handler = CreateHandler();
        var message = CreateMessage(start: string.Empty);

        await handler.HandleAsync(message, CancellationToken.None);

        _stateMachine.Verify(sm => sm.CreateAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        _stateMachine.Verify(sm => sm.AdvanceAsync(It.IsAny<int>(), It.IsAny<TaskState>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithNewProcess_ShouldCreateAndAdvance()
    {
        const int newPid = 42;
        var handler = CreateHandler();
        var message = CreateMessage(pid: 0);

        _stateMachine.Setup(sm => sm.CreateAsync(message.CorrelationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(newPid);
        _stateMachine.Setup(sm => sm.AdvanceAsync(newPid, TaskState.RouteFinding, message.CorrelationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await handler.HandleAsync(message, CancellationToken.None);

        _stateMachine.Verify(sm => sm.CreateAsync(message.CorrelationId, It.IsAny<CancellationToken>()), Times.Once);
        _stateMachine.Verify(sm => sm.AdvanceAsync(newPid, TaskState.RouteFinding, message.CorrelationId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithExistingProcess_ShouldAdvanceWithoutCreate()
    {
        const int pid = 99;
        var handler = CreateHandler();
        var message = CreateMessage(pid: pid);

        _stateMachine.Setup(sm => sm.AdvanceAsync(pid, TaskState.RouteFinding, message.CorrelationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await handler.HandleAsync(message, CancellationToken.None);

        _stateMachine.Verify(sm => sm.CreateAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        _stateMachine.Verify(sm => sm.AdvanceAsync(pid, TaskState.RouteFinding, message.CorrelationId, It.IsAny<CancellationToken>()), Times.Once);
    }
}

