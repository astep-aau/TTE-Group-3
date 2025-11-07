using System;
using System.Diagnostics;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;
using PathfindingService.Domain.Entities;
using PathfindingService.Domain.Entities.Events;
using FluentValidation;
#nullable enable
namespace PathfindingService.Features.CreateRoute;

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
        _ = _handler; // reference the handler to avoid 'assigned but never used' warning until handler is implemented
    }

    public async Task Consume(ConsumeContext<CreateProcessEvent> context)
    {
        var evt = context.Message;

        _logger.LogInformation("Received CreateProcessEvent for CorrelationId={CorrelationId}", evt.CorrelationId);

        // validate the incoming event
        var validation = await _validator.ValidateAsync(evt, context.CancellationToken);
        if (!validation.IsValid)
        {
            _logger.LogWarning("CreateProcessEvent validation failed: {Errors}", validation.Errors);
            return; // drop/ack the message - or move to dead-letter depending on your policy
        }

        var (origLat, origLon) = ParseLatLon(evt.Origin);
        var (destLat, destLon) = ParseLatLon(evt.Destination);

        if (origLat is null || origLon is null)
            _logger.LogWarning("Event Origin could not be parsed as lat,lon: {Origin}", evt.Origin);

        var originNode = (origLat is not null && origLon is not null) ? CreateOsmNode(origLat, origLon) : null;
        _logger.LogInformation("Created origin OSM node: {Node}", originNode);

        if (destLat is null || destLon is null)
            _logger.LogWarning("Event Destination could not be parsed as lat,lon: {Destination}", evt.Destination);

        var destinationNode = (destLat is not null && destLon is not null) ? CreateOsmNode(destLat, destLon) : null;
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

        // TODO: Create Handler and replace 'SaveRouteAsync()' function with handler function
        // await _handler.SaveRouteAsync(route, context.CancellationToken);
    }

    private string? CreateOsmNode(string? lat, string? lon)
    {
        if (string.IsNullOrWhiteSpace(lat) || string.IsNullOrWhiteSpace(lon)) return null;

        const string pythonPath = "python3";
        const string scriptPath = "Helper/CreateOsmNode.py";

        var psi = new ProcessStartInfo
        {
            FileName = pythonPath,
            Arguments = $"{scriptPath} {lat} {lon}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process();
        process.StartInfo = psi;
        process.Start();
        var output = process.StandardOutput.ReadToEnd().Trim();    // output: string
        var error = process.StandardError.ReadToEnd();      // error: string
        process.WaitForExit();

        if (string.IsNullOrEmpty(error)) return output;

        _logger.LogInformation("Error creating OSM node: {Error}", error);
        return null;
    }

    // Parse lat/lon from evt.Origin and evt.Destination. Expecting format like "lat,lon".
    private (string? lat, string? lon) ParseLatLon(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return (null, null);
        var parts = s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2) return (null, null);
        return (parts[0], parts[1]);
    }
}