using translator_service.Domain.Entities;

namespace translator_service.Features.CreateProcess;

public class CreateProcessCommand
{
    public int Id { get; init; }
    public Guid CorrelationId { get; init; } = Guid.Empty;
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public bool routeCreated { get; set; } = false;
    public bool timeEstimated { get; set; } = false;
    
    public CreateProcessCommand() { }
    
    public CreateProcessCommand(CreateProcessRequest request)
    {
        Id = request.Id;
        CorrelationId = request.CorrelationId;
        Origin = request.Origin;
        Destination = request.Destination;
        CreatedAt = request.CreatedAt;
        routeCreated = request.routeCreated;
        timeEstimated = request.timeEstimated;
    }
}


