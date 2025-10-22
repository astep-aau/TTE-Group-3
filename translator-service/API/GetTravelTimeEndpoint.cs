using Microsoft.AspNetCore.Mvc;
using translator_service.Features.GetTravelTime;
using System.Diagnostics;
using translator_service.Domain.Entities;

namespace translator_service.Endpoints;

[ApiController]
[Route("api/[controller]")]
public class GetTravelTimeEndpoint : ControllerBase
{
    private readonly GetTravelTimeHandler _handler;
    private readonly ILogger<GetTravelTimeEndpoint> _logger;

    public GetTravelTimeEndpoint(GetTravelTimeHandler handler, ILogger<GetTravelTimeEndpoint> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    [HttpGet("{correlationId:guid}")]
    public async Task<IActionResult> GetTravelTime(Guid correlationId, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        }))
        {
            try
            {
                _logger.LogInformation("Received travel time request");

                var travelTime = await _handler.GetByCorrelationIdAsync(correlationId, cancellationToken);

                if (travelTime is null)
                {
                    stopwatch.Stop();
                    _logger.LogWarning("Travel time not found. Duration: {Duration}ms", stopwatch.ElapsedMilliseconds);
                    return NotFound(new { message = "Travel time not found", correlationId });
                }

                await _handler.MarkAsDeliveredAsync(correlationId, cancellationToken);

                stopwatch.Stop();
                _logger.LogInformation("Travel time delivered successfully. Duration: {Duration}ms", stopwatch.ElapsedMilliseconds);

                return Ok(new
                {
                    CorrelationId = travelTime.CorrelationId,
                    TravelTimeMinutes = travelTime.TravelTimeMinutes
                });
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Request cancelled after {Duration}ms", stopwatch.ElapsedMilliseconds);
                return StatusCode(499, new { message = "Request cancelled", correlationId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception occurred while processing travel time request. Duration: {Duration}ms",
                    stopwatch.ElapsedMilliseconds);

                return StatusCode(500, new
                {
                    message = "An error occurred while processing your request",
                    correlationId
                });
            }
        }
    }
}