using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using StateService.Domain.Value;
using StateService.Features.RouteEstimationCompleted;
using StateService.Infrastructure.Services;
using Xunit;

namespace StateService.Tests;

public class RouteEstimationCompletedHandlerTests
{
    private readonly Mock<IStateMachineService> _stateMachine = new();
    private readonly Mock<ILogger<RouteEstimationCompletedHandler>> _logger = new();

    private RouteEstimationCompletedHandler CreateHandler() =>
        new(_stateMachine.Object, _logger.Object);

    private static RouteEstimationCompletedMessage CreateMessage(Guid? correlationId = null) =>
        new(
            correlationId ?? Guid.NewGuid(),
            "Copenhagen",
            "Odense",
            145.2,
            95.3,
            new List<RouteCoordinate> { new(55.6761, 12.5683) });

    [Fact]
    public async Task HandleAsync_WhenCorrelationNotFound_ShouldSkipAdvances()
    {
        var correlation = Guid.NewGuid();
        var message = CreateMessage(correlation);
        _stateMachine.Setup(s => s.GetPidByCorrelationIdAsync(correlation.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);

        await CreateHandler().HandleAsync(message, CancellationToken.None);

        _stateMachine.Verify(s => s.AdvanceAsync(It.IsAny<int>(), It.IsAny<TaskState>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenFirstAdvanceFails_ShouldNotAdvanceToModelLoading()
    {
        const int pid = 42;
        var correlation = Guid.NewGuid();
        var correlationString = correlation.ToString();
        var message = CreateMessage(correlation);

        _stateMachine.Setup(s => s.GetPidByCorrelationIdAsync(correlationString, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pid);
        _stateMachine.Setup(s => s.AdvanceAsync(pid, TaskState.TimeEstimation, correlationString, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await CreateHandler().HandleAsync(message, CancellationToken.None);

        _stateMachine.Verify(s => s.AdvanceAsync(pid, TaskState.TimeEstimation, correlationString, It.IsAny<CancellationToken>()), Times.Once);
        _stateMachine.Verify(s => s.AdvanceAsync(pid, TaskState.ModelLoading, correlationString, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenAdvancesSucceed_ShouldAdvanceThroughModelLoading()
    {
        const int pid = 101;
        var correlation = Guid.NewGuid();
        var correlationString = correlation.ToString();
        var message = CreateMessage(correlation);

        _stateMachine.Setup(s => s.GetPidByCorrelationIdAsync(correlationString, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pid);
        _stateMachine.Setup(s => s.AdvanceAsync(pid, TaskState.TimeEstimation, correlationString, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _stateMachine.Setup(s => s.AdvanceAsync(pid, TaskState.ModelLoading, correlationString, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await CreateHandler().HandleAsync(message, CancellationToken.None);

        _stateMachine.Verify(s => s.AdvanceAsync(pid, TaskState.TimeEstimation, correlationString, It.IsAny<CancellationToken>()), Times.Once);
        _stateMachine.Verify(s => s.AdvanceAsync(pid, TaskState.ModelLoading, correlationString, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenModelLoadingAdvanceFails_ShouldStillAttemptAdvance()
    {
        const int pid = 77;
        var correlation = Guid.NewGuid();
        var correlationString = correlation.ToString();
        var message = CreateMessage(correlation);

        _stateMachine.Setup(s => s.GetPidByCorrelationIdAsync(correlationString, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pid);
        _stateMachine.Setup(s => s.AdvanceAsync(pid, TaskState.TimeEstimation, correlationString, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _stateMachine.Setup(s => s.AdvanceAsync(pid, TaskState.ModelLoading, correlationString, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await CreateHandler().HandleAsync(message, CancellationToken.None);

        _stateMachine.Verify(s => s.AdvanceAsync(pid, TaskState.ModelLoading, correlationString, It.IsAny<CancellationToken>()), Times.Once);
    }
}

