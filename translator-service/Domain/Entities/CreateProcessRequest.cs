
namespace translator_service.Domain.Entities;
public class CreateProcessRequest
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
}

