using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PathfindingService.Domain.Entities;

namespace PathfindingService.Features.CreateRoute;

public class CreateRouteHandler
{
    private readonly ILogger<CreateRouteHandler> _logger;

    public CreateRouteHandler(ILogger<CreateRouteHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(ProcessPayload payload, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Handling route for ProcessId={ProcessId}", payload.ProcessId);
        // implement save/processing logic here
        
        
        
        return Task.CompletedTask;
    }
}