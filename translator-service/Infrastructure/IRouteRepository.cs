using translator_service.Domain.Entities;

namespace translator_service.Features.GetRoute;

public interface IRouteRepository
{
    Task<RouteResult?> GetRouteAsync(Guid correlationId, CancellationToken ct);
    Task SaveRouteAsync(RouteResult route, CancellationToken ct);

}