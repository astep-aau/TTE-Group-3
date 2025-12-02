using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RouteEstimationService.Domain.Entities;
using RouteEstimationService.Domain.Entities.Events;
using RouteEstimationService.Helper;
using RouteEstimationService.Features.EstimateTime;

namespace RouteEstimationService.Features.CreateRoute;

public class CreateRouteHandler : ICreateRouteHandler
{
    private readonly ILogger<CreateRouteHandler> _logger;
    private readonly IRouteMadeEmitter _emitter;
    private readonly EstimateTimeHandler _estimateTimeHandler;

    public CreateRouteHandler(ILogger<CreateRouteHandler> logger, IRouteMadeEmitter emitter, EstimateTimeHandler estimateTimeHandler)
    {
        _logger = logger;
        _emitter = emitter;
        _estimateTimeHandler = estimateTimeHandler;
    }

    public async Task HandleAsync(ProcessPayload payload)
    {
        _logger.LogInformation("[RouteHandler] Handling route for ProcessId={ProcessId}", payload.ProcessId);

        // Parse latitude and longitude from origin and destination
        (string origLat, string origLon) = ParseLatLon(payload.Origin);
        (string destLat, string destLon) = ParseLatLon(payload.Destination);

        if (origLat is null || origLon is null)
        {
            _logger.LogWarning("[RouteHandler] Invalid origin coordinates: {Origin} for ProcessId={ProcessId}", payload.Origin, payload.ProcessId);
            throw new ArgumentException("Invalid origin coordinates");
        }

        if (destLat is null || destLon is null)
        {
            _logger.LogWarning("[RouteHandler] Invalid destination coordinates: {Destination} for ProcessId={ProcessId}", payload.Destination, payload.ProcessId);
            throw new ArgumentException("Invalid destination coordinates");
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
            throw new ApplicationException("Failed to determine nearest nodes", ex);
        }
        _logger.LogInformation("[RouteHandler] Nearest nodes found: OriginNodeId={OriginNodeId}, DestinationNodeId={DestinationNodeId} for ProcessId={ProcessId}",
            originNodeId, destinationNodeId, payload.ProcessId);

        // Find the shortest route using A*
        var routeResult = ShortestRouteFinder.ShortestRoute(originNodeId, destinationNodeId);

        if (!routeResult.IsSuccess)
        {
            _logger.LogWarning("[RouteHandler] No route found for ProcessId={ProcessId}", payload.ProcessId);
            throw new ApplicationException("No route found");
        }
        if (routeResult.Value.EdgeIds.Count == 0 || routeResult.Value.NodeIds.Count < 2)
        {
            _logger.LogWarning("[RouteHandler] Empty route path for ProcessId={ProcessId}", payload.ProcessId);
            throw new ApplicationException("Empty route path");
        }
        var route = routeResult.Value;
        _logger.LogInformation(
            "[RouteHandler] Route processed successfully for ProcessId={ProcessId} and created the route with RouteId={RouteId}:\n{Route}",
            payload.ProcessId, route.RouteId, route);
        
        // Handle the time estimation
        _logger.LogInformation("[RouteHandler] Estimating time for ProcessId={ProcessId}, RouteId={RouteId}, ModelVersion={ModelVersion}", payload.ProcessId, routeResult.Value.RouteId, payload.ModelVersion);
        var estimationResult = _estimateTimeHandler.EstimateTime(route, payload.ModelVersion, payload.TimeOfTravel);
        if (!estimationResult.IsSuccess)
        {
            string errorMessage = string.Join(", ", estimationResult.Errors.Select(e => e.Message).Where(m => !string.IsNullOrWhiteSpace(m)));
            if (string.IsNullOrWhiteSpace(errorMessage))
                errorMessage = string.Join(", ", estimationResult.Errors);

            _logger.LogError("[RouteHandler] Time estimation failed for ProcessId={ProcessId}, RouteId={RouteId}: {Errors}", payload.ProcessId,
                route.RouteId, errorMessage);
            throw new ApplicationException("Time estimation failed: " + errorMessage);
        }
        routeResult = estimationResult;
        _logger.LogInformation(
            "[RouteHandler] Time estimation completed for ProcessId={ProcessId}, RouteId={RouteId} with EstimatedTime={EstimatedTime} seconds",
            payload.ProcessId, routeResult.Value.RouteId, routeResult.Value.EstimatedTimeSeconds);
        
        _logger.LogInformation(
            "[RouteHandler] Mapping Node IDs to coordinates for ProcessId={ProcessId}, RouteId={RouteId}",
            payload.ProcessId, routeResult.Value.RouteId);

        try
        {
            routeResult.Value.Path = NodeIdToCoordinates.Map(routeResult.Value.NodeIds)
                .ConvertAll(coord => new RouteCoordinate
                {
                    Latitude = coord.Lat,
                    Longitude = coord.Lon,
                });
        }
        catch (KeyNotFoundException e)
        {
            _logger.LogError(e, "[RouteHandler] Failed to map Node IDs to coordinates for ProcessId={ProcessId}, RouteId={RouteId}", payload.ProcessId, routeResult.Value.RouteId);
            throw new ApplicationException("Failed to map Node IDs to coordinates", e);
        }
        
        var routeMadeEvent = new RouteMadeEvent
        {
            Id = payload.ProcessId,
            CorrelationId = payload.CorrelationId,
            Origin = payload.Origin, 
            Destination = payload.Destination,
            DistanceKm = routeResult.Value.DistanceKm, 
            TravelTimeSeconds = routeResult.Value.EstimatedTimeSeconds, 
            Path = routeResult.Value.Path 
        };
        
        await _emitter.EmitCreateProcessEventAsync(routeMadeEvent);
        return;

        // Function for parsing lat/lon for origin and destination (expecting "lat,lon")
        static (string lat, string lon) ParseLatLon(string s)
        {
            string[] parts = s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return parts.Length >= 2 ? (parts[0], parts[1]) : (null, null);
        }
    }
}