using FluentResults;
using Microsoft.Extensions.Logging;
using RouteEstimationService.Domain.Entities;
using RouteEstimationService.Helper;

namespace RouteEstimationService.Features.CreateRoute;

public class CreateRouteHandler
{
    private readonly ILogger<CreateRouteHandler> _logger;

    public CreateRouteHandler(ILogger<CreateRouteHandler> logger)
    {
        _logger = logger;
    }

    public Result<ShortestRouteFinder.RouteResult> HandleAsync(ProcessPayload payload)
    {
        _logger.LogInformation("Handling route for ProcessId={ProcessId}", payload.ProcessId);

        // Find nearest nodes for origin and destination
        string originNodeId = NearestNodeFinder.NearestNode(payload.Origin, payload.Origin);
        string destinationNodeId = NearestNodeFinder.NearestNode(payload.Destination, payload.Destination);

        // Find the shortest route using A*
        var routeResult = ShortestRouteFinder.ShortestRoute(originNodeId, destinationNodeId);

        if (!routeResult.IsSuccess)
        {
            _logger.LogWarning("No route found for ProcessId={ProcessId}", payload.ProcessId);
            return Result.Fail("No route found");
        }
        if (routeResult.Value.EdgeIds.Count == 0 || routeResult.Value.NodeIds.Count < 2)
        {
            _logger.LogWarning("Empty route path for ProcessId={ProcessId}", payload.ProcessId);
            return Result.Fail("Empty route path");
        }
        
        _logger.LogInformation("Route found for ProcessId={ProcessId}", payload.ProcessId);
        return Result.Ok(routeResult.Value);
    }
}