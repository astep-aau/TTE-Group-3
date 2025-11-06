using System;
using System.Diagnostics;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;
using PathfindingService.Domain.Entities;
using PathfindingService.Domain.Entities.Events;
namespace PathfindingService.Features.CreateRoute;

public class CreateRouteConsumer : IConsumer<CreateProcessEvent>
{
    private readonly CreateRouteHandler _handler;
    private readonly ILogger<CreateRouteConsumer> _logger;
    
    // TODO: Add a validator
    // private CreateProcessValidator _validator;

    public CreateRouteConsumer(CreateRouteHandler handler, ILogger<CreateRouteConsumer> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CreateProcessEvent> context)
    {
        var evt = context.Message;

        _logger.LogInformation("Received CreateProcessEvent for CorrelationId={CorrelationId}", evt.CorrelationId);

        var (origLat, origLon) = ParseLatLon(evt.Origin);
        var (destLat, destLon) = ParseLatLon(evt.Destination);

        if (origLat is null && origLon is null)
            _logger.LogWarning("Event Origin could not be parsed as lat,lon: {Origin}", evt.Origin);

        var originNode = CreateOsmNode(origLat, origLon);
        _logger.LogInformation("Created origin OSM node: {Node}", originNode);

        if (destLat is not null && destLon is not null)
            _logger.LogWarning("Event Destination could not be parsed as lat,lon: {Destination}", evt.Destination);

        var destinationNode = CreateOsmNode(destLat, destLon);
        _logger.LogInformation("Created destination OSM node: {Node}", destinationNode);

        var payload = new ProcessPayload
        {
            ProcessId = evt.ProcessId,
            CorrelationId = evt.CorrelationId,
            Origin = originNode,
            Destination = destinationNode,
            TimeOfTravel = evt.TimeOfTravel,
            CreatedAt = evt.CreatedAt,
            ModelVersion = evt.ModelVersion
        };

        // TODO: Create Handler and replace 'SaveRouteAsync()' function with handler function
        // await _handler.SaveRouteAsync(route, context.CancellationToken);
    }

    private string CreateOsmNode(string lat, string lon)
    {
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
        var output = process.StandardOutput.ReadToEnd();    // output: string
        var error = process.StandardError.ReadToEnd();      // error: string
        process.WaitForExit();

        if (string.IsNullOrEmpty(error)) return output;

        _logger.LogInformation("Error creating OSM node: {Error}", error);
        return null;
    }

    // Parse lat/lon from evt.Origin and evt.Destination. Expecting format like "lat,lon".
    private (string lat, string lon) ParseLatLon(string s)
    {
        var parts = s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2) return (null, null);
        return (parts[0], parts[1]);
    }
}