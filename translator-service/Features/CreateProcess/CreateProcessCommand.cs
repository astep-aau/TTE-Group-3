
using translator_service.Domain.Entities;

namespace translator_service.Features.CreateProcess;

public class CreateProcessCommand
{
    public int Id { get; init; }
    public Guid CorrelationId { get; init; } = Guid.Empty;
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public TimeOnly TimeOfTravel { get; init; } = TimeOnly.MinValue;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public string ModelVersion { get; set; } = string.Empty;
    public bool routeCreated { get; set; } = false;
    public bool timeEstimated { get; set; } = false;
    
    public CreateProcessCommand() { }
    
    public CreateProcessCommand(CreateProcessRequest request)
    {
        Id = request.Id;
        CorrelationId = request.CorrelationId;
        Origin = request.Origin;
        Destination = request.Destination;
        TimeOfTravel = request.TimeOfTravel;
        ModelVersion = request.ModelVersion;
        CreatedAt = request.CreatedAt;
        routeCreated = request.routeCreated;
        timeEstimated = request.timeEstimated;
    }
}


