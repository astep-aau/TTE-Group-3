using System;
using FluentResults;
using Microsoft.Extensions.Logging;
using RouteEstimationService.Domain.Entities;
using RouteEstimationService.Helper;
using RouteEstimationService.Features.EstimateTime;

namespace RouteEstimationService.Features.CreateRoute;

public class CreateRouteHandler
{
    private readonly ILogger<CreateRouteHandler> _logger;

    public CreateRouteHandler(ILogger<CreateRouteHandler> logger)
    {
        _logger = logger;
    }

    public Result<RouteResult> HandleAsync(ProcessPayload payload)
    {
        _logger.LogInformation("[RouteHandler] Handling route for ProcessId={ProcessId}", payload.ProcessId);

        // Parse latitude and longitude from origin and destination
        (string origLat, string origLon) = ParseLatLon(payload.Origin);
        (string destLat, string destLon) = ParseLatLon(payload.Destination);

        if (origLat is null || origLon is null)
        {
            _logger.LogWarning("[RouteHandler] Invalid origin coordinates: {Origin} for ProcessId={ProcessId}", payload.Origin, payload.ProcessId);
            return Result.Fail("Invalid origin coordinates");
        }

        if (destLat is null || destLon is null)
        {
            _logger.LogWarning("[RouteHandler] Invalid destination coordinates: {Destination} for ProcessId={ProcessId}", payload.Destination, payload.ProcessId);
            return Result.Fail("Invalid destination coordinates");
        }

        // Find nearest nodes for origin and destination
        string originNodeId;
        string destinationNodeId;
        try
        {
            originNodeId = NearestNodeFinder.NearestNode(origLat, origLon);
            destinationNodeId = NearestNodeFinder.NearestNode(destLat, destLon);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RouteHandler] Failed to determine nearest nodes for ProcessId={ProcessId}", payload.ProcessId);
            return Result.Fail($"Failed to determine nearest nodes: {ex.Message}");
        }
        _logger.LogInformation("[RouteHandler] Nearest nodes found: OriginNodeId={OriginNodeId}, DestinationNodeId={DestinationNodeId} for ProcessId={ProcessId}",
            originNodeId, destinationNodeId, payload.ProcessId);

        // Find the shortest route using A*
        var routeResult = ShortestRouteFinder.ShortestRoute(originNodeId, destinationNodeId);

        if (!routeResult.IsSuccess)
        {
            _logger.LogWarning("[RouteHandler] No route found for ProcessId={ProcessId}", payload.ProcessId);
            return Result.Fail("No route found");
        }
        if (routeResult.Value.EdgeIds.Count == 0 || routeResult.Value.NodeIds.Count < 2)
        {
            _logger.LogWarning("[RouteHandler] Empty route path for ProcessId={ProcessId}", payload.ProcessId);
            return Result.Fail("Empty route path");
        }
        _logger.LogInformation(
            "[RouteHandler] Route processed successfully for ProcessId={ProcessId} and created the route with RouteId={RouteId}:\n{Route}",
            routeResult.Value.RouteId, payload.ProcessId, routeResult.Value);
        
        // Handle the time estimation
        _logger.LogInformation("[RouteHandler] Estimating time for ProcessId={ProcessId}, RouteId={RouteId}", payload.ProcessId, routeResult.Value.RouteId);
        var estimateTimeHandler = new EstimateTimeHandler(_logger);
        routeResult = estimateTimeHandler.EstimateTime(routeResult.Value);
        if (!routeResult.IsSuccess)
        {
            _logger.LogError("[RouteHandler] Time estimation failed for ProcessId={ProcessId}, RouteId={RouteId}: {Errors}", payload.ProcessId,
                routeResult.Value.RouteId, string.Join(", ", routeResult.Errors));
            return Result.Fail("Time estimation failed");
        }
        _logger.LogInformation(
            "[RouteHandler] Time estimation completed for ProcessId={ProcessId}, RouteId={RouteId} with EstimatedTime={EstimatedTime} seconds",
            payload.ProcessId, routeResult.Value.RouteId, routeResult.Value.EstimatedTimeSeconds);
        
        return Result.Ok(routeResult.Value);

        // Function for parsing lat/lon for origin and destination (expecting "lat,lon")
        static (string lat, string lon) ParseLatLon(string s)
        {
            string[] parts = s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return parts.Length >= 2 ? (parts[0], parts[1]) : (null, null);
        }
    }
}