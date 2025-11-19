using FluentResults;
using RouteEstimationService.Domain.Entities;

namespace RouteEstimationService.Features.CreateRoute;

public interface ICreateRouteHandler
{
    Result<RouteResult> HandleAsync(ProcessPayload payload);
}