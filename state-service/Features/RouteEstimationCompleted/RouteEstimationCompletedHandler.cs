using Microsoft.Extensions.Logging;
using StateService.Domain.Value;
using StateService.Infrastructure.Services;
using System.Threading;
using System.Threading.Tasks;

namespace StateService.Features.RouteEstimationCompleted
{
    public class RouteEstimationCompletedHandler
    {
        private readonly IStateMachineService _stateMachine;
        private readonly ILogger<RouteEstimationCompletedHandler> _logger;

        public RouteEstimationCompletedHandler(IStateMachineService stateMachine, ILogger<RouteEstimationCompletedHandler> logger)
        {
            _stateMachine = stateMachine;
            _logger = logger;
        }

        public async Task HandleAsync(RouteEstimationCompletedMessage message, CancellationToken ct = default)
        {
            var correlationId = message.CorrelationId.ToString();
            var pid = await _stateMachine.GetPidByCorrelationIdAsync(correlationId, ct);
            if (!pid.HasValue)
            {
                _logger.LogWarning("RouteEstimationCompleted correlation not found correlationId={CorrelationId}", correlationId);
                return;
            }

            var advancedToTime = await _stateMachine.AdvanceAsync(pid.Value, TaskState.TimeEstimation, correlationId, ct);
            if (!advancedToTime)
            {
                _logger.LogWarning("RouteEstimationCompleted transition to TimeEstimation rejected pid={Pid} correlationId={CorrelationId}", pid.Value, correlationId);
                return;
            }

            var advancedToModel = await _stateMachine.AdvanceAsync(pid.Value, TaskState.ModelLoading, correlationId, ct);
            if (!advancedToModel)
            {
                _logger.LogWarning("RouteEstimationCompleted transition to ModelLoading rejected pid={Pid} correlationId={CorrelationId}", pid.Value, correlationId);
            }
        }
    }
}

