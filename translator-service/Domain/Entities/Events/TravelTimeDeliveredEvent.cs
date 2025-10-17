namespace translator_service.Domain.Events;

public class TravelTimeDeliveredEvent
{
    public Guid CorrelationId { get; set; } = Guid.Empty;
}