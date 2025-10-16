namespace translator_service.Domain.Events;

public class RouteMadeEvent
{
    public Guid CorrelationId { get; set; }
    public string Origin { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public double DistanceKm { get; set; }
    public List<RouteCoordinateDto> Path { get; set; } = new();
}

public class RouteCoordinateDto
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}