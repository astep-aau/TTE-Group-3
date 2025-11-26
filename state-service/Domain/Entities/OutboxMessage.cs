using System;

namespace StateService.Domain.Entities
{
    public class OutboxMessage
    {
        public long Id { get; set; }
        public DateTime OccurredOn { get; set; } = DateTime.UtcNow;
        public string Type { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
        public DateTime? ProcessedAt { get; set; }
        public string? CorrelationId { get; set; }
    }
}
