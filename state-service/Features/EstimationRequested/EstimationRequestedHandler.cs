using StateService.Infrastructure.Services;
using StateService.Domain.Value;
using Microsoft.Extensions.Logging;

namespace StateService.Features.EstimationRequested
{
    public class EstimationRequestedHandler
    {
        private readonly IStateMachineService _stateMachine;
        private readonly ILogger<EstimationRequestedHandler> _logger;
        public EstimationRequestedHandler(IStateMachineService stateMachine, ILogger<EstimationRequestedHandler> logger)
        {
            _stateMachine = stateMachine;
            _logger = logger;
        }

        public async System.Threading.Tasks.Task HandleAsync(EstimationRequestedMessage message, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(message.Start) || string.IsNullOrWhiteSpace(message.End))
            {
                _logger.LogWarning("Invalid estimation request pid={Pid} correlationId={CorrelationId} reason=MissingData", message.Pid, message.CorrelationId);
                return;
            }
            int pid = message.Pid == 0 ? await _stateMachine.CreateAsync(message.CorrelationId, ct) : message.Pid;
            await _stateMachine.AdvanceAsync(pid, TaskState.RouteFinding, message.CorrelationId, ct);
        }
    }
}
