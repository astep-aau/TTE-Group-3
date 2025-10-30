
namespace translator_service.Domain.Events;

using translator_service.Domain.Entities;

public class CreateProcessEvent
{
    public int ProcessId { get; init; }
    public Guid CorrelationId { get; init; }
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public TimeOnly TimeOfTravel { get; init; } = TimeOnly.MinValue;
    public DateTime CreatedAt { get; init; }
    public string ModelVersion { get; set; } = string.Empty;

    // Convenience factory to build the event from an incoming request/entity
    public static CreateProcessEvent From(CreateProcessRequest req)
        => new()
        {
            ProcessId = req.Id,
            CorrelationId = req.CorrelationId,
            Origin = req.Origin,
            Destination = req.Destination,
            TimeOfTravel = req.TimeOfTravel,
            CreatedAt = req.CreatedAt,
            ModelVersion = req.ModelVersion
        };
}