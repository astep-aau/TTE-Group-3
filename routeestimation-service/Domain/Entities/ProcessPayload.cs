using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.JavaScript;

namespace RouteEstimationService.Domain.Entities;

public record ProcessPayload
{
    public int Id { get; init; }
    public int ProcessId { get; init; }
    public Guid CorrelationId { get; init; } = Guid.Empty;
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public TimeOnly TimeOfTravel { get; init; } = TimeOnly.MinValue;
    public DateTime CreatedAt { get; init; }
    public string ModelVersion { get; set; } = string.Empty;
}
