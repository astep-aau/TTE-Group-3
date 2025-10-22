using translator_service.Domain.Entities;

namespace translator_service.Infrastructure
{
    public interface ITravelTimeRepository
    {
        Task<TravelTimeResult?> GetTravelTimeByCorrelationIdAsync(Guid correlationId, CancellationToken ct);
        Task UpdateTravelTimeAsync(TravelTimeResult travelTime, CancellationToken ct);
    }
}