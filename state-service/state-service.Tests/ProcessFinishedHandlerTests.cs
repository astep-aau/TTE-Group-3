using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using StateService.Domain.Value;
using StateService.Features.ProcessFinished;
using StateService.Infrastructure.Services;
using Xunit;

namespace StateService.Tests;

public class ProcessFinishedHandlerTests
{
    private readonly Mock<IStateMachineService> _stateMachine = new();
    private readonly Mock<ILogger<ProcessFinishedHandler>> _logger = new();

    private ProcessFinishedHandler CreateHandler() =>
        new(_stateMachine.Object, _logger.Object);

    private static ProcessFinishedMessage CreateMessage(int pid = 10, string summary = "ok") =>
        new(pid, summary, "corr-999");

    [Fact]
    public async Task HandleAsync_ShouldAdvanceToFinished()
    {
        var message = CreateMessage();
        _stateMachine.Setup(sm => sm.AdvanceAsync(message.Pid, TaskState.Finished, message.CorrelationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await CreateHandler().HandleAsync(message, CancellationToken.None);

        _stateMachine.Verify(sm => sm.AdvanceAsync(message.Pid, TaskState.Finished, message.CorrelationId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenAdvanceFails_ShouldStillAttemptAdvanceOnce()
    {
        var message = CreateMessage();
        _stateMachine.Setup(sm => sm.AdvanceAsync(message.Pid, TaskState.Finished, message.CorrelationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await CreateHandler().HandleAsync(message, CancellationToken.None);

        _stateMachine.Verify(sm => sm.AdvanceAsync(message.Pid, TaskState.Finished, message.CorrelationId, It.IsAny<CancellationToken>()), Times.Once);
    }
}

