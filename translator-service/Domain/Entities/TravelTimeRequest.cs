namespace translator_service.Domain.Entities;

public class TravelTimeRequest
{
    public Guid Id { get; set; }
    public Guid CorrelationId { get; set; }
    
    public bool IsProcessed { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}