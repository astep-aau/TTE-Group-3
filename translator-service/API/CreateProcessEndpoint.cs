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
        var stopwatch = Stopwatch.StartNew();
        
        
        
        stopwatch.Stop();
        _logger.LogInformation($"CreateProcessAsync {stopwatch.ElapsedMilliseconds}ms");
        return Ok();
    }

}




/*public static void MapCreateProcessEndpoint(WebApplication app)
{
    var group = app.MapGroup("/api/processes");

    group.MapPost("/", ([FromBody] JsonElement payload) =>
        {
            if (!TryGetName(payload, out var name))
                return Results.BadRequest(new
                    { error = "The 'name' field is required and must be a non-empty string." });

            var id = Guid.NewGuid();
            var location = $"/api/processes/{id}";
            return Results.Created(location, new { id, name });
        })
        .WithName("CreateProcess")
        .Produces(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest);
}

private static bool TryGetName(JsonElement payload, out string name)
{
    name = string.Empty;

    if (!payload.TryGetProperty("name", out var nameProp))
        return false;

    if (nameProp.ValueKind != JsonValueKind.String)
        return false;

    var raw = nameProp.GetString()?.Trim();
    if (string.IsNullOrWhiteSpace(raw))
        return false;

    name = raw!;
    return true;
}
}
*/

