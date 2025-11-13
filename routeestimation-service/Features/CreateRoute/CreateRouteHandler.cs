using System.Threading.Tasks;
using FluentResults;
using Microsoft.Extensions.Logging;
using RouteEstimationService.Domain.Entities;

namespace RouteEstimationService.Features.CreateRoute;

public class CreateRouteHandler
{
    private readonly ILogger<CreateRouteHandler> _logger;

    public CreateRouteHandler(ILogger<CreateRouteHandler> logger)
    {
        _logger = logger;
    }

    public Result HandleAsync(ProcessPayload payload)
    {
        _logger.LogInformation("Handling route for ProcessId={ProcessId}", payload.ProcessId);
        // implement save/processing logic here
        
        //TODO: Use the new 2 helper function to create route and return the result
        
        return Result.Ok();
    }
}