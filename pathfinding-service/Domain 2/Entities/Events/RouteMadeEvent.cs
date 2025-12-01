using System;
using System.Collections.Generic;

namespace PathfindingService.Domain.Entities.Events;

public record RouteMadeEvent
{
    public Guid CorrelationId { get; init; }
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public double DistanceKm { get; init; }
    public List<RouteCoordinateDto> Path { get; set; } = new();
}

public class RouteCoordinateDto
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}