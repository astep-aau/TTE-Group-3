using Microsoft.Extensions.Logging;
using StateService.Domain.Value;
using StateService.Infrastructure.Services;

namespace StateService.Features.TimeEstimated
{
    public class TimeEstimatedHandler
    {
        private readonly IStateMachineService _stateMachine;
        private readonly ILogger<TimeEstimatedHandler> _logger;
        public TimeEstimatedHandler(IStateMachineService stateMachine, ILogger<TimeEstimatedHandler> logger)
        {
            _stateMachine = stateMachine;
            _logger = logger;
        }

        public async System.Threading.Tasks.Task HandleAsync(TimeEstimatedMessage message, CancellationToken ct = default)
        {
            if (message.Seconds <= 0)
            {
                _logger.LogWarning("Invalid time-estimated message pid={Pid} correlationId={CorrelationId} reason=NonPositiveSeconds", message.Pid, message.CorrelationId);
                return;
            }
            var advanced = await _stateMachine.AdvanceAsync(message.Pid, TaskState.ModelLoading, message.CorrelationId, ct);
            if (!advanced)
            {
                _logger.LogWarning("TimeEstimated transition rejected (Pid={Pid})", message.Pid);
            }
        }
    }
}
