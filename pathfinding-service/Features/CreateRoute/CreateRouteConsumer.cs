using MassTransit;
using Microsoft.Extensions.Logging;
using PathfindingService.Domain;
using PathfindingService.Domain.Events;

namespace PathfindingService.Features.CreateRoute;

public class CreateRouteConsumer : IConsumer<CreateRouteEvent>
{
    private readonly CreateRouteHandler _handler;
    private readonly ILogger<CreateRouteConsumer> _logger;

    public CreateRouteConsumer(CreateRouteHandler handler, ILogger<CreateRouteConsumer> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CreateRouteEvent> context)
    {
        var evt = context.Message;

        _logger.LogInformation("Received RouteMadeEvent for CorrelationId={CorrelationId}", evt.CorrelationId);

        var route = new RouteResult
        {
            CorrelationId = evt.CorrelationId,
            Origin = evt.Origin,
            Destination = evt.Destination,
            DistanceKm = evt.DistanceKm,
            Path = evt.Path.Select(p => new RouteCoordinate
            {
                Latitude = p.Latitude,
                Longitude = p.Longitude
            }).ToList()
        };

        await _handler.SaveRouteAsync(route, context.CancellationToken);
    }
}