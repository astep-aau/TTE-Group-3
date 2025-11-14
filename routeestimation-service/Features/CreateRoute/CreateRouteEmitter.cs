using System;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using RouteEstimationService.Domain.Entities.Events;

namespace RouteEstimationService.Features.CreateRoute;

public class CreateRouteEmitter
{
    private readonly IBus _bus;

    public CreateRouteEmitter(IBus bus)
    {
        _bus = bus;
    }
        
    public async Task EmitCreateProcessEventAsync(RouteMadeEvent routeMadeEvent, CancellationToken ct = default)
    {
        Console.WriteLine("[Emitter] Emitting RouteMadeEvent for CorrelationId={0}", routeMadeEvent.CorrelationId);
        await _bus.Publish(routeMadeEvent, ct);
    }
}