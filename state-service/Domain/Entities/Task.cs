using System;
using StateService.Domain.Value;

namespace StateService.Domain.Entities
{
    public class Task
    {
        public int Pid { get; set; }
        public TaskState CurrentState { get; set; } = TaskState.New;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }

    public class ProcessLog
    {
        public int LogId { get; set; }
        public int Pid { get; set; }
        public DateTime FinishedAt { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "finished";
        public string? CorrelationId { get; set; }
    }
}
