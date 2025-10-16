using translator_service.Domain.Entities;

namespace translator_service.Features.GetRoute;

public interface IRouteRepository
{
    Task<RouteResult?> GetRouteAsync(Guid correlationId, string origin, string destination, CancellationToken ct);
    Task SaveRouteAsync(RouteResult route, CancellationToken ct);
}