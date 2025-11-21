using System.Threading.Tasks;
using RouteEstimationService.Domain.Entities;

namespace RouteEstimationService.Features.CreateRoute;

public interface ICreateRouteHandler
{
    Task HandleAsync(ProcessPayload payload);
}