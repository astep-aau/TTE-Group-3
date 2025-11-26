using MassTransit;
using translator_service.Domain.Events;

namespace translator_service.Features.GetRoute;

public class RouteDeliveredEmitter
{
    private readonly IBus _bus;
    private readonly ILogger<RouteDeliveredEmitter> _logger;

    public RouteDeliveredEmitter(IBus bus, ILogger<RouteDeliveredEmitter> logger)
    {
        _bus = bus;
        _logger = logger;
    }

    public async Task EmitAsync(Guid correlationId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Publishing RouteDeliveredEvent for CorrelationId={CorrelationId}", correlationId);

        await _bus.Publish(new RouteDeliveredEvent
        {
            CorrelationId = correlationId,
            DeliveredAtUtc = DateTime.UtcNow
        }, cancellationToken);
    }
}

