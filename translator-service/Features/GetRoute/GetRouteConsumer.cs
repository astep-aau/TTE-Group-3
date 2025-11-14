using MassTransit;
using Microsoft.Extensions.Logging;
using translator_service.Domain.Events;
using translator_service.Domain.Entities;

namespace translator_service.Features.GetRoute;

public class GetRouteConsumer : IConsumer<RouteMadeEvent>
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

    public async Task Consume(ConsumeContext<RouteMadeEvent> context)
    {
        var evt = context.Message;

        _logger.LogInformation(
            "Received RouteMadeEvent for CorrelationId={CorrelationId} including travel time",
            evt.CorrelationId);

        var route = new RouteResult
        {
            CorrelationId = evt.CorrelationId,
            Origin = evt.Origin,
            Destination = evt.Destination,
            DistanceKm = evt.DistanceKm,
            TravelTimeMinutes = evt.TravelTimeMinutes,
            Path = evt.Path.Select(p => new RouteCoordinate
            {
                Latitude = p.Latitude,
                Longitude = p.Longitude
            }).ToList()
        };

        await _routeHandler.SaveRouteAsync(route, context.CancellationToken);
    }
}