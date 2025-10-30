using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using StateService.Domain.Entities;
using StateService.Domain.Value;
using StateService.Infrastructure.Persistence;
using System.Text.Json;
using LiveTaskEntity = StateService.Domain.Entities.Task;

namespace StateService.Infrastructure.Services
{
    public interface IStateMachineService
    {
        System.Threading.Tasks.Task<int> CreateAsync(string? correlationId, CancellationToken ct = default);
        System.Threading.Tasks.Task<bool> AdvanceAsync(int pid, TaskState nextState, string? correlationId, CancellationToken ct = default);
        System.Threading.Tasks.Task<TaskState?> GetStateAsync(int pid, CancellationToken ct = default);
    }

    public class StateMachineService : IStateMachineService
    {
        private static readonly Dictionary<TaskState, TaskState?> AllowedPrevious = new()
        {
            { TaskState.New, null },
            { TaskState.RouteFinding, TaskState.New },
            { TaskState.TimeEstimation, TaskState.RouteFinding },
            { TaskState.ModelLoading, TaskState.TimeEstimation },
            { TaskState.UiReturn, TaskState.ModelLoading },
            { TaskState.Finished, TaskState.UiReturn }
        };

        private readonly StateDbContext _db;
        private readonly ILogger<StateMachineService> _logger;

        public StateMachineService(StateDbContext db, ILogger<StateMachineService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async System.Threading.Tasks.Task<int> CreateAsync(string? correlationId, CancellationToken ct = default)
        {
            var task = new LiveTaskEntity();
            _db.Tasks.Add(task);
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Process created pid={Pid} correlationId={CorrelationId}", task.Pid, correlationId);
            return task.Pid;
        }

        public async System.Threading.Tasks.Task<bool> AdvanceAsync(int pid, TaskState nextState, string? correlationId, CancellationToken ct = default)
        {
            var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Pid == pid, ct);
            if (task == null)
            {
                _logger.LogWarning("Advance failed pid={Pid} state={State} reason=NotFound correlationId={CorrelationId}", pid, nextState, correlationId);
                return false;
            }

            if (!AllowedPrevious.TryGetValue(nextState, out var requiredPrev))
            {
                _logger.LogWarning("Advance failed pid={Pid} state={State} reason=InvalidTarget correlationId={CorrelationId}", pid, nextState, correlationId);
                return false;
            }

            if (requiredPrev is not null && task.CurrentState != requiredPrev)
            {
                _logger.LogWarning("Advance failed pid={Pid} current={Current} attempted={Next} requiredPrev={RequiredPrev} correlationId={CorrelationId}", pid, task.CurrentState, nextState, requiredPrev, correlationId);
                return false;
            }

            task.CurrentState = nextState;
            task.UpdatedAt = DateTime.UtcNow;

            if (nextState == TaskState.Finished)
            {
                _db.ProcessLogs.Add(new ProcessLog
                {
                    Pid = task.Pid,
                    FinishedAt = DateTime.UtcNow,
                    Status = "finished",
                    CorrelationId = correlationId
                });
                _db.Tasks.Remove(task);
            }

            EnqueueOutbox(pid, nextState, correlationId);

            try
            {
                await _db.SaveChangesAsync(ct);
                _logger.LogInformation("Advance succeeded pid={Pid} newState={State} correlationId={CorrelationId}", pid, nextState, correlationId);
                return true;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Advance concurrency conflict pid={Pid} state={State} correlationId={CorrelationId}", pid, nextState, correlationId);
                return false;
            }
        }

        public async System.Threading.Tasks.Task<TaskState?> GetStateAsync(int pid, CancellationToken ct = default)
        {
            var task = await _db.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.Pid == pid, ct);
            return task?.CurrentState;
        }

        private void EnqueueOutbox(int pid, TaskState newState, string? correlationId)
        {
            var message = new OutboxMessage
            {
                Type = "state.changed",
                CorrelationId = correlationId,
                Payload = JsonSerializer.Serialize(new { pid, state = newState.ToString(), occurredAt = DateTime.UtcNow })
            };
            _db.OutboxMessages.Add(message);
        }
    }
}
