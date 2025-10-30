using Microsoft.Extensions.Logging;
using StateService.Domain.Value;
using StateService.Infrastructure.Services;

namespace StateService.Features.RouteCalculated
{
    public class RouteCalculatedHandler
    {
        private readonly IStateMachineService _stateMachine;
        private readonly ILogger<RouteCalculatedHandler> _logger;
        public RouteCalculatedHandler(IStateMachineService stateMachine, ILogger<RouteCalculatedHandler> logger)
        {
            _stateMachine = stateMachine;
            _logger = logger;
        }

        public async System.Threading.Tasks.Task HandleAsync(RouteCalculatedMessage message, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(message.RouteJson))
            {
                _logger.LogWarning("Invalid route-calculated message pid={Pid} correlationId={CorrelationId} reason=EmptyRoute", message.Pid, message.CorrelationId);
                return;
            }
            var advanced = await _stateMachine.AdvanceAsync(message.Pid, TaskState.TimeEstimation, message.CorrelationId, ct);
            if (!advanced)
            {
                _logger.LogWarning("RouteCalculated transition rejected (Pid={Pid})", message.Pid);
            }
        }
    }
}
