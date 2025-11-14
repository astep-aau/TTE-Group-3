using System;
using System.Collections.Generic;

namespace RouteEstimationService.Domain.Entities.Events;

public record RouteMadeEvent
{
    public Guid RouteId { get; init; }
    public Guid CorrelationId { get; init; }
    public int ProcessId { get; init; }
    public List<string> NodeIds { get; init; } = new List<string>();
    public List<int> EdgeIds { get; init; } = new List<int>();
    public double EstimatedTimeSeconds { get; set; } = 0;
}
