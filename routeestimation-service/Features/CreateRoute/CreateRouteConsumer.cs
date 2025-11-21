using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;
using FluentValidation;
using RouteEstimationService.Domain.Entities;
using RouteEstimationService.Domain.Entities.Events;
using ValidationResult = FluentValidation.Results.ValidationResult;     // to avoid conflict with System.ComponentModel.DataAnnotations.ValidationResult

namespace RouteEstimationService.Features.CreateRoute;

public class CreateRouteConsumer : IConsumer<CreateProcessEvent>
{
    private readonly ICreateRouteHandler _handler;
    private readonly ILogger<CreateRouteConsumer> _logger;
    private readonly IValidator<CreateProcessEvent> _validator;

    public CreateRouteConsumer(ICreateRouteHandler handler, ILogger<CreateRouteConsumer> logger, IValidator<CreateProcessEvent> validator)
    {
        _handler = handler;
        _logger = logger;
        _validator = validator;
    }

    public async Task Consume(ConsumeContext<CreateProcessEvent> context)
    {
        var evt = context.Message;

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
            TimeOfTravel = evt.TimeOfTravel,    // TODO: Use this for time-dependent routing and time estimation
            CreatedAt = evt.CreatedAt,
            ModelVersion = evt.ModelVersion     // Used in the translator service, and is just passed through here
        };

        // Handle the route creation
        _logger.LogInformation("[Consumer] Processing route for ProcessId={ProcessId}", payload.ProcessId);
        try
        {
            await _handler.HandleAsync(payload);
            _logger.LogInformation("[Consumer] Route created successfully for ProcessId={ProcessId}", payload.ProcessId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Consumer] Failed to create route for ProcessId={ProcessId}", payload.ProcessId);
            throw; // Re-throw to trigger MassTransit retry/error handling
        }
    }
}