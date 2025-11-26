using System.Threading;
using System.Threading.Tasks;
using RouteEstimationService.Domain.Entities.Events;

namespace RouteEstimationService.Features.CreateRoute;

public interface IRouteMadeEmitter
{
    Task EmitCreateProcessEventAsync(RouteMadeEvent routeMadeEvent, CancellationToken ct = default);
}

