using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;
using FluentValidation;
using FluentResults;
using RouteEstimationService.Domain.Entities;
using RouteEstimationService.Domain.Entities.Events;
using RouteEstimationService.Helper;
using ValidationResult = FluentValidation.Results.ValidationResult;     // to avoid conflict with System.ComponentModel.DataAnnotations.ValidationResult

namespace RouteEstimationService.Features.CreateRoute;

public class CreateRouteConsumer : IConsumer<CreateProcessEvent>
{
    private readonly CreateRouteHandler _handler;
    private readonly ILogger<CreateRouteConsumer> _logger;
    private readonly IValidator<CreateProcessEvent> _validator;

    public CreateRouteConsumer(CreateRouteHandler handler, ILogger<CreateRouteConsumer> logger, IValidator<CreateProcessEvent> validator)
    {
        _handler = handler;
        _logger = logger;
        _validator = validator;
    }

    public async Task Consume(ConsumeContext<CreateProcessEvent> context)
    {
        var evt = context.Message;

        _logger.LogInformation("Received CreateProcessEvent for CorrelationId={CorrelationId}", evt.CorrelationId);

        // Validate the incoming event
        ValidationResult validation = await _validator.ValidateAsync(evt);
        if (!validation.IsValid)
        {
            _logger.LogWarning("CreateProcessEvent validation failed: {Errors}", validation.Errors);
            return; // Drop/ack the message - or move to dead-letter depending on your policy
        }

        // Extract lat/lon from Origin and Destination
        (string origLat, string origLon) = ParseLatLon(evt.Origin);
        (string destLat, string destLon) = ParseLatLon(evt.Destination);
        
        if (origLat is null || origLon is null)
            _logger.LogWarning("Event Origin could not be parsed as lat,lon: {Origin}", evt.Origin);

        // Find nearest OSM nodes for origin
        string originNode = (origLat is not null && origLon is not null) ? NearestNodeFinder.NearestNode(origLat, origLon) : null;
        _logger.LogInformation("Created origin OSM node: {Node}", originNode);

        if (destLat is null || destLon is null)
            _logger.LogWarning("Event Destination could not be parsed as lat,lon: {Destination}", evt.Destination);

        // Find nearest OSM nodes for destination
        string destinationNode = (destLat is not null && destLon is not null) ? NearestNodeFinder.NearestNode(destLat, destLon) : null;
        _logger.LogInformation("Created destination OSM node: {Node}", destinationNode);

        var payload = new ProcessPayload
        {
            ProcessId = evt.ProcessId,
            CorrelationId = evt.CorrelationId,
            Origin = originNode ?? string.Empty,
            Destination = destinationNode ?? string.Empty,
            TimeOfTravel = evt.TimeOfTravel,
            CreatedAt = evt.CreatedAt,
            ModelVersion = evt.ModelVersion
        };

        // Handle the route creation
        _logger.LogInformation("Processing route for ProcessId={ProcessId}", payload.ProcessId);
        var result = _handler.HandleAsync(payload);

        if (!result.IsSuccess)
        {
            _logger.LogError("Route processing failed for ProcessId={ProcessId}: {Errors}", payload.ProcessId,
                string.Join(", ", result.Errors));
            return;
        }
        var route = result.Value;
        _logger.LogInformation(
            "Route processed successfully for ProcessId={ProcessId} and created the route:\n{Route}",
            payload.ProcessId, route);

        // Call training service to vector embed edges
        _logger.LogInformation("Vector embedding the edges for ProcessId={ProcessId}", payload.ProcessId);
        var embeddedEdges = await new HttpClient().PostAsync(
            "http://127.0.0.1:8000/Python/predict-time",
            new StringContent(JsonSerializer.Serialize(route.EdgeIds),
                Encoding.UTF8,
                "application/json"));

        if (!embeddedEdges.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to vector embed edges for ProcessId={ProcessId}. StatusCode: {StatusCode}",
                payload.ProcessId, embeddedEdges.StatusCode);
            return;
        }
        string responseContent = await embeddedEdges.Content.ReadAsStringAsync();
        _logger.LogInformation("Successfully vector embedded edges for ProcessId={ProcessId}: {Response}",
            payload.ProcessId, responseContent);

        // Add embeddedEdges to route
        try
        {
            var embeddedEdgesObj = JsonSerializer.Deserialize<object>(responseContent);
            route.GetType().GetProperty("EmbeddedEdges")?.SetValue(route, embeddedEdgesObj);
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to add embeddedEdges to route for ProcessId={ProcessId}: {Error}",
                payload.ProcessId, ex.Message);
        }
    }

    // Parse lat/lon from origin and destination. Expecting format "lat,lon".
    private static (string lat, string lon) ParseLatLon(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return (null, null);
        string[] parts = s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length < 2 ? (null, null) : (parts[0], parts[1]);
    }
}