using Microsoft.AspNetCore.Mvc;
using translator_service.Domain.Entities;
using translator_service.Features.GetRoute;
using System.Diagnostics;

namespace translator_service.Endpoints;

[ApiController]
[Route("api/route")]
public class GetRouteEndpoint : ControllerBase
{
    private readonly GetRouteHandler _handler;
    private readonly ILogger<GetRouteEndpoint> _logger;

    public GetRouteEndpoint(GetRouteHandler handler, ILogger<GetRouteEndpoint> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetRouteAsync([FromQuery] Guid correlationId, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        }))
        {
            try
            {
                _logger.LogInformation("Received route request");

                var command = new GetRouteCommand(correlationId);
                var validator = new GetRouteValidator();
                var validation = validator.Validate(command);

                if (!validation.IsValid)
                {
                    _logger.LogWarning("Validation failed with {ErrorCount} errors: {Errors}",
                        validation.Errors.Count,
                        string.Join(", ", validation.Errors.Select(e => e.ErrorMessage)));

                    return BadRequest(new
                    {
                        errors = validation.Errors.Select(e => e.ErrorMessage),
                        correlationId = correlationId
                    });
                }

                var result = await _handler.HandleAsync(command, ct);

                stopwatch.Stop();

                if (result is null)
                {
                    _logger.LogWarning("Route not found. Duration: {Duration}ms", stopwatch.ElapsedMilliseconds);
                    return NotFound(new { message = "Route not found", correlationId = correlationId });
                }

                _logger.LogInformation("Route retrieved successfully. Duration: {Duration}ms", stopwatch.ElapsedMilliseconds);

                // Map domain entity to DTO that only exposes lat/lon for path entries
                var dto = new RouteResultDto
                {
                    CorrelationId = result.CorrelationId,
                    Origin = result.Origin,
                    Destination = result.Destination,
                    DistanceKm = result.DistanceKm,
                    Path = result.Path?.Select(p => new RouteCoordinateDto
                    {
                        Latitude = p.Latitude,
                        Longitude = p.Longitude
                    }).ToList() ?? new List<RouteCoordinateDto>()
                };

                return Ok(dto);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Request cancelled after {Duration}ms", stopwatch.ElapsedMilliseconds);
                return StatusCode(499, new { message = "Request cancelled", correlationId = correlationId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception occurred while processing route request. Duration: {Duration}ms",
                    stopwatch.ElapsedMilliseconds);

                return StatusCode(500, new
                {
                    message = "An error occurred while processing your request",
                    correlationId = correlationId
                });
            }
        }
    }
}