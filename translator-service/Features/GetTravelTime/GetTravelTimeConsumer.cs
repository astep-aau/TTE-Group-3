using MassTransit;
using Microsoft.Extensions.Logging;
using translator_service.Domain.Entities;
using translator_service.Domain.Events;

namespace translator_service.Features.GetTravelTime;

public class GetTravelTimeConsumer : IConsumer<TravelTimeCalculatedEvent>
{
    private readonly GetTravelTimeHandler _handler;
    private readonly ILogger<GetTravelTimeConsumer> _logger;

    public GetTravelTimeConsumer(GetTravelTimeHandler handler, ILogger<GetTravelTimeConsumer> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<TravelTimeCalculatedEvent> context)
    {
        var evt = context.Message;

        _logger.LogInformation("Received TravelTimeCalculatedEvent for CorrelationId={CorrelationId}", evt.CorrelationId);

        var travelTime = new TravelTimeResult
        {
            CorrelationId = evt.CorrelationId,
            TravelTimeMinutes = evt.TravelTimeMinutes
        };

        await _handler.SaveTravelTimeAsync(travelTime, context.CancellationToken);
    }
}