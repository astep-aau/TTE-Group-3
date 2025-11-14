namespace translator_service.Domain.Events;

public class RouteDeliveredEvent
{
    public Guid CorrelationId { get; set; } = Guid.Empty;
    public DateTime DeliveredAtUtc { get; set; } = DateTime.UtcNow;
}

