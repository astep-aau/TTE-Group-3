using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace PathfindingService.Features.CreateRoute;

public class CreateRouteHandler
{
    private readonly ILogger<CreateRouteHandler> _logger;

    public CreateRouteHandler(ILogger<CreateRouteHandler> logger)
    {
        _logger = logger;
    }

    public async Task Fiddle()
    {

    }
}