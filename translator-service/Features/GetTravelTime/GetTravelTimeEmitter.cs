using MassTransit;
using translator_service.Domain.Events;

namespace translator_service.Features.GetTravelTime;

public class TravelTimeDeliveredEmitter
{
    private readonly IBus _bus;

    public TravelTimeDeliveredEmitter(IBus bus)
    {
        _bus = bus;
    }

    public async Task EmitDeliveredEventAsync(Guid correlationId)
    {
        await _bus.Publish(new TravelTimeDeliveredEvent
        {
            CorrelationId = correlationId
        });
    }
}

