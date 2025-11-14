using System;
using System.Collections.Generic;
using System.Linq;

namespace RouteEstimationService.Domain.Entities;

public record RouteResult
{
    public Guid RouteId { get; } = Guid.NewGuid();
    public List<string> NodeIds { get; init; } = new List<string>();
    public List<int> EdgeIds { get; init; } = new List<int>();
    public List<List<double>> EmbeddedEdges { get; set; } = new List<List<double>>();
    public double EstimatedTimeSeconds { get; set; } = 0;

    // Provide a readable string so logging prints list contents instead of the collection type name
    public override string ToString()
    {
        string nodes = NodeIds is { Count: > 0 } ? string.Join(", ", NodeIds) : string.Empty;
        string edges = EdgeIds is { Count: > 0 } ? string.Join(", ", EdgeIds) : string.Empty;
        string embedded = EmbeddedEdges is { Count: > 0 } ? string.Join("; ", EmbeddedEdges.Select(list => "[" + string.Join(", ", list) + "]")) : string.Empty;
        return $"RouteResult {{ RouteId = {RouteId}, NodeIds = [{nodes}], EdgeIds = [{edges}], EmbeddedEdges = [{embedded}], EstimatedTimeSeconds = {EstimatedTimeSeconds} }}";
    }
}