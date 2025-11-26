namespace translator_service.Domain.Entities;

public record RouteResultRequest
{
        
    public Guid CorrelationId { get; set; } = Guid.Empty;
    
    public string Origin { get; set; } = string.Empty;
    
    public string Destination { get; set; } = string.Empty;
}