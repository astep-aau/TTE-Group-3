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
using RouteEstimationService.Features.EstimateTime;
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
        await HandleEventAsync(context.Message);
    }
    
    public async Task HandleEventAsync(CreateProcessEvent evt)
    {
        // var evt = context.Message;

        _logger.LogInformation("[Consumer] Received CreateProcessEvent for CorrelationId={CorrelationId}", evt.CorrelationId);

        // Validate the incoming event
        ValidationResult validation = await _validator.ValidateAsync(evt);
        if (!validation.IsValid)
        {
            _logger.LogWarning("[Consumer] CreateProcessEvent validation failed: {Errors}", validation.Errors);
            return; // Drop/ack the message - or move to dead-letter depending on your policy
        }
        _logger.LogInformation("[Consumer] CreateProcessEvent validation succeeded for CorrelationId={CorrelationId}", evt.CorrelationId);
        
        
        var payload = new ProcessPayload
        {
            ProcessId = evt.ProcessId,
            CorrelationId = evt.CorrelationId,
            Origin = evt.Origin,
            Destination = evt.Destination,
            TimeOfTravel = evt.TimeOfTravel,
            CreatedAt = evt.CreatedAt,
            ModelVersion = evt.ModelVersion
        };

        // Handle the route creation
        _logger.LogInformation("[Consumer] Processing route for ProcessId={ProcessId}", payload.ProcessId);
        var result = _handler.HandleAsync(payload);

        if (!result.IsSuccess)
        {
            _logger.LogError("[Consumer] Route processing failed for ProcessId={ProcessId}: {Errors}", payload.ProcessId,
                string.Join(", ", result.Errors));
            return;
        }
        _logger.LogInformation(
            "[Consumer] Route processed successfully for ProcessId={ProcessId} and created the route with RouteId={RouteId}:\n{Route}", result.Value.RouteId,
            payload.ProcessId, result.Value);
        
        // Handle the time estimation
        _logger.LogInformation("[Consumer] Estimating time for ProcessId={ProcessId}, RouteId={RouteId}", payload.ProcessId, result.Value.RouteId);
        var estimateTimeHandler = new EstimateTimeHandler(_logger);
        var timeEstimationResult = estimateTimeHandler.EstimateTime(result.Value);
        if (!timeEstimationResult.IsSuccess)
        {
            _logger.LogError("[Consumer] Time estimation failed for ProcessId={ProcessId}, RouteId={RouteId}: {Errors}", payload.ProcessId,
                result.Value.RouteId, string.Join(", ", timeEstimationResult.Errors));
            return;
        }
        _logger.LogInformation(
            "[Consumer] Time estimation completed for ProcessId={ProcessId}, RouteId={RouteId} with EstimatedTime={EstimatedTime} seconds",
            payload.ProcessId, result.Value.RouteId, timeEstimationResult.Value.EstimatedTimeSeconds);
    }
}