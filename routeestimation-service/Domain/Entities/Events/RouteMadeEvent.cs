using System;
using System.Collections.Generic;

namespace RouteEstimationService.Domain.Entities.Events;

public record RouteMadeEvent
{
    public int Id { get; init; }
    public Guid CorrelationId { get; init; } = Guid.Empty;
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;

    public double DistanceKm { get; init; }
    public double TravelTimeMinutes { get; init; }
    
    public IReadOnlyList<RouteCoordinate> Path { get; init; }
}

public record RouteCoordinate
{
    public double Latitude { get; init; }
    public double Longitude { get; init; }
}