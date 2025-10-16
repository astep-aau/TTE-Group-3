using System.Runtime.InteropServices.JavaScript;

namespace translator_service.Domain.Entities;

public record RouteResult
{
    public int Id { get; init; }
    public Guid CorrelationId { get; init; } = Guid.Empty;
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;

    public double DistanceKm { get; init; }
    
    public List<RouteCoordinate> Path { get; init; } = new();
}

public record RouteCoordinate
{
    public int Id { get; init; }
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public int RouteResultId { get; init; } 
}