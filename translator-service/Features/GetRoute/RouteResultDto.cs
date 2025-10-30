using System.Collections.Generic;

namespace translator_service.Features.GetRoute;

public record RouteCoordinateDto
{
    public double Latitude { get; init; }
    public double Longitude { get; init; }
}

public record RouteResultDto
{
    public System.Guid CorrelationId { get; init; }
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public double DistanceKm { get; init; }
    public List<RouteCoordinateDto> Path { get; init; } = new();
}
