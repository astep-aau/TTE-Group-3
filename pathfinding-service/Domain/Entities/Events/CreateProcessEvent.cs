using System;

namespace PathfindingService.Domain.Entities.Events;

public class CreateProcessEvent
{
    public int ProcessId { get; init; }
    public Guid CorrelationId { get; init; }
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public TimeOnly TimeOfTravel { get; init; } = TimeOnly.MinValue;
    public DateTime CreatedAt { get; init; }
    public string ModelVersion { get; set; } = string.Empty;
}