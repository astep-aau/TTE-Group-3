using System;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;
using RouteEstimationService.Domain.Entities.Events;

namespace RouteEstimationService.Features.CreateRoute;

public class RouteMadeEmitter : IRouteMadeEmitter
{
    private readonly ILogger<RouteMadeEmitter> _logger;
    private readonly IBus _bus;

    public RouteMadeEmitter(IBus bus, ILogger<RouteMadeEmitter> logger)
    {
        _bus = bus;
        _logger = logger;
    }
        
    public async Task EmitCreateProcessEventAsync(RouteMadeEvent routeMadeEvent, CancellationToken ct = default)
    {
        _logger.LogInformation("[Emitter] Emitting RouteMadeEvent for CorrelationId={CorrelationId}", routeMadeEvent.CorrelationId);
        await _bus.Publish(routeMadeEvent, ct);
    }
}