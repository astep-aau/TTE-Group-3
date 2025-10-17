namespace translator_service.Domain.Events;

public class TravelTimeCalculatedEvent
{
    public Guid CorrelationId { get; set; }
    public double TravelTimeMinutes { get; set; }
}