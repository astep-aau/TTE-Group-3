using MassTransit;
using Microsoft.Extensions.Logging;
using Entities = translator_service.Domain.Entities;
using Events = RouteEstimationService.Domain.Entities.Events;

namespace translator_service.Features.GetRoute;

public class GetRouteConsumer : IConsumer<Events.RouteMadeEvent>
{
    private readonly GetRouteHandler _routeHandler;
    private readonly ILogger<GetRouteConsumer> _logger;

    public GetRouteConsumer(
        GetRouteHandler routeHandler,
        ILogger<GetRouteConsumer> logger)
    {
        _routeHandler = routeHandler;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<Events.RouteMadeEvent> context)
    {
        var evt = context.Message;

        _logger.LogInformation(
            "Received RouteMadeEvent for CorrelationId={CorrelationId} including travel time",
            evt.CorrelationId);

        var route = new Entities.RouteResult
        {
            CorrelationId = evt.CorrelationId,
            Origin = evt.Origin,
            Destination = evt.Destination,
            DistanceKm = evt.DistanceKm,
            TravelTimeMinutes = evt.TravelTimeMinutes,
            Path = evt.Path.Select(p => new Entities.RouteCoordinate
            {
                Latitude = p.Latitude,
                Longitude = p.Longitude
            }).ToList()
        };

        await _routeHandler.SaveRouteAsync(route, context.CancellationToken);
    }
}