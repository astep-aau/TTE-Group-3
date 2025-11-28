namespace RouteEstimationService.Domain.Entities.Events;

public record RouteMadeEvent(
    int Id,
    Guid CorrelationId,
    string Origin,
    string Destination,
    double DistanceKm,
    double TravelTimeSeconds,
    IReadOnlyList<RouteCoordinate> Path);

public record RouteCoordinate(double Latitude, double Longitude);