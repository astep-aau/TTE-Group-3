namespace RouteEstimationService.Domain.Entities.Events;

public record RouteMadeEvent(
    Guid CorrelationId,
    string Origin,
    string Destination,
    double DistanceKm,
    double TravelTimeMinutes,
    IReadOnlyList<RouteCoordinate> Path);

public record RouteCoordinate(double Latitude, double Longitude);