using System.Diagnostics;
using System.Text.Json;
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

        _handler.HandleAsync(req, ct);
        
        stopwatch.Stop();
        _logger.LogInformation($"CreateProcessAsync {stopwatch.ElapsedMilliseconds}ms");
        return Ok();
    }

}

