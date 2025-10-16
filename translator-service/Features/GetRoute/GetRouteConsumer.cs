using translator_service.Domain.Entities;
using MassTransit;
using Microsoft.Extensions.Logging;
using translator_service.Domain.Events;

namespace translator_service.Features.GetRoute;

public class GetRouteConsumer : IConsumer<RouteMadeEvent>
{
    private readonly GetRouteHandler _handler;
    private readonly ILogger<GetRouteConsumer> _logger;

    public GetRouteConsumer(GetRouteHandler handler, ILogger<GetRouteConsumer> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<RouteMadeEvent> context)
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