using Microsoft.Extensions.Logging;
using StateService.Domain.Value;
using StateService.Infrastructure.Services;

namespace StateService.Features.ProcessFinished
{
    public class ProcessFinishedHandler
    {
        private readonly IStateMachineService _stateMachine;
        private readonly ILogger<ProcessFinishedHandler> _logger;
        public ProcessFinishedHandler(IStateMachineService stateMachine, ILogger<ProcessFinishedHandler> logger)
        {
            _stateMachine = stateMachine;
            _logger = logger;
        }

        public async System.Threading.Tasks.Task HandleAsync(ProcessFinishedMessage message, CancellationToken ct = default)
        {
            var advanced = await _stateMachine.AdvanceAsync(message.Pid, TaskState.Finished, message.CorrelationId, ct);
            if (!advanced)
            {
                _logger.LogWarning("ProcessFinished transition rejected (Pid={Pid})", message.Pid);
            }
        }
    }
}
