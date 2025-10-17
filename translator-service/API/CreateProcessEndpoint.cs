using System.Diagnostics;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using translator_service.Domain.Entities;
using translator_service.Features.CreateProcess;

namespace translator_service.API;

[Route("api/processes")]
[ApiController]

public class CreateProcessEndpoint : ControllerBase
{
    private readonly CreateProcessHandler _handler;
    private readonly ILogger<CreateProcessEndpoint> _logger;
    private static readonly CreateProcessValidator Validator = new();

    public CreateProcessEndpoint(CreateProcessHandler handler, ILogger<CreateProcessEndpoint> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> CreateProcessAsync([FromBody] CreateProcessRequest req, CancellationToken ct)
    {
        // TODO: Create logging scope and error handling like in GetRouteEndpoint
        var stopwatch = Stopwatch.StartNew();

        using (_logger.BeginScope(new Dictionary<string, object>
               {
                   ["CorrelationId"] = req.CorrelationId,
                   ["Origin"] = req.Origin,
                   ["Destination"] = req.Destination,
                   ["CreatedAt"] = req.CreatedAt
               }))
        {
            try
            {
                _logger.LogInformation("Received create process request");
                
                var command = new CreateProcessCommand(req);
                var validation = await Validator.ValidateAsync(command);

                if (!validation.IsValid)
                {
                    _logger.LogWarning("Validation failed with {ErrorCount} errors: {Errors}",
                        validation.Errors.Count,
                        string.Join(", ", validation.Errors.Select(e => e.ErrorMessage)));

                    return BadRequest(new
                    {
                        errors = validation.Errors.Select(e => e.ErrorMessage),
                        correlationId = req.CorrelationId
                    });
                }
                
                await _handler.HandleAsync(command, ct);
                stopwatch.Stop();
                
                _logger.LogInformation("Process created successfully. Duration: {Duration}ms",
                    stopwatch.ElapsedMilliseconds);
                return Ok(req);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Route retrieval operation was cancelled after {Duration}ms", 
                    stopwatch.ElapsedMilliseconds);
                return StatusCode(499, new { message = "Request cancelled", correlationId = req.CorrelationId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving route from repository. Duration: {Duration}ms", 
                    stopwatch.ElapsedMilliseconds);
                
                return StatusCode(500, new 
                { 
                    message = "An error occurred while processing your request",
                    correlationId = req.CorrelationId
                });
            }
        }
    }
}

