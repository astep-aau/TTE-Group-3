namespace translator_service.Domain.Entities;

public class TravelTimeResult
{
    public Guid Id { get; set; }
    public Guid CorrelationId { get; set; }
    public double TravelTimeMinutes { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsDelivered { get; set; }
}