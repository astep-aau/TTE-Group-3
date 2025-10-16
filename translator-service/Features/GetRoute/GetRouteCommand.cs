using translator_service.Domain.Entities;

namespace translator_service.Features.GetRoute;

public class GetRouteCommand
{
    public Guid CorrelationId { get; set; }
    public string Origin { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;

    public GetRouteCommand() { }

    public GetRouteCommand(RouteResultRequest request)
    {
        CorrelationId = request.CorrelationId;
        Origin = request.Origin;
        Destination = request.Destination;
    }
}