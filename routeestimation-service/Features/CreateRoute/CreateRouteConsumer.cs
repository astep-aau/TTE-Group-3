using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentResults;
using MassTransit;
using Microsoft.Extensions.Logging;
using FluentValidation;
using RouteEstimationService.Domain.Entities;
using RouteEstimationService.Domain.Entities.Events;
using ValidationResult = FluentValidation.Results.ValidationResult;     // to avoid conflict with System.ComponentModel.DataAnnotations.ValidationResult

namespace RouteEstimationService.Features.CreateRoute;

public class CreateRouteConsumer : IConsumer<CreateProcessEvent>
{
    private readonly CreateRouteHandler _handler;
    private readonly ILogger<CreateRouteConsumer> _logger;
    private readonly IValidator<CreateProcessEvent> _validator;
    // private readonly CreateRouteEmitter _emitter;

    public CreateRouteConsumer(CreateRouteHandler handler, ILogger<CreateRouteConsumer> logger, IValidator<CreateProcessEvent> validator/*, CreateRouteEmitter emitter*/)
    {
        _handler = handler;
        _logger = logger;
        _validator = validator;
        // _emitter = emitter;
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
        var route = _handler.HandleAsync(payload);
        
        var routeMadeEvent = new RouteMadeEvent
        {
           Id = payload.ProcessId,
           CorrelationId = payload.CorrelationId,
           Origin = payload.Origin, 
           Destination = payload.Destination,
           DistanceKm = route.Value.DistanceKm, 
           TravelTimeMinutes = route.Value.EstimatedTimeSeconds, 
           Path = route.Value.Path 
        };

        if (!route.IsSuccess)
        {
            _logger.LogWarning("[Consumer] Route creation failed for ProcessId={ProcessId}: {Errors}",
                payload.ProcessId, string.Join(", ", route.Errors));
        }
        else
        {
            _logger.LogInformation("[Consumer] Route created successfully for ProcessId={ProcessId}:\n{Route}",
                payload.ProcessId, route.Value);
        }
        // await _emitter.EmitCreateProcessEventAsync(routeMadeEvent);
    }
}