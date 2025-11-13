using System;
using System.Collections.Generic;

namespace RouteEstimationService.Domain.Entities;

public record RouteResult
{
    public Guid RouteId { get; } = Guid.NewGuid();
    public List<string> NodeIds { get; init; } = [];
    public List<int> EdgeIds { get; init; } = [];
    public List<double> EmbeddedEdges { get; set; } = [];
    public double EstimatedTimeSeconds { get; set; } = 0;
}