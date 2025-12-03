using System;

namespace RouteEstimationService.Domain.Entities.Events;

public record CreateProcessEvent
{
    public int ProcessId { get; init; }
    public Guid CorrelationId { get; init; }
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public TimeOnly? TimeOfTravel { get; init; }
    public DateTime CreatedAt { get; init; }
    public string ModelVersion { get; init; } = string.Empty;
}
