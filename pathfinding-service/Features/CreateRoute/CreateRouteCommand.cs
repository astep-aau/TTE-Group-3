using pathfinding_service.Domain.Entities;

namespace pathfinding_service.Features.CreateRoute;

public class CreateRouteCommand
{
    public Guid CorrelationId { get; set; }
    public string Origin { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;

    public CreateRouteCommand() { }

    public CreateRouteCommand(PathRequest request)
    {
        CorrelationId = request.CorrelationId;
        Origin = request.Origin;
        Destination = request.Destination;
    }

    public CreateRouteCommand(Guid correlationId, string origin, string destination)
    {
        CorrelationId = correlationId;
        Origin = origin;
        Destination = destination;
    }
}