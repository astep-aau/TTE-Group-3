using translator_service.Domain.Entities;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace translator_service.Features.GetRoute;

public class GetRouteHandler
{
    private readonly IRouteRepository _repository;
    private readonly ILogger<GetRouteHandler> _logger;

    public GetRouteHandler(IRouteRepository repository, ILogger<GetRouteHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<RouteResult?> HandleAsync(GetRouteCommand command, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = command.CorrelationId
        }))
        {
            try
            {
                _logger.LogInformation("Starting route retrieval");

                var route = await _repository.GetRouteAsync(command.CorrelationId, ct);

                stopwatch.Stop();

                if (route is null)
                {
                    _logger.LogWarning("Route not found in repository. Query duration: {Duration}ms", 
                        stopwatch.ElapsedMilliseconds);
                    return null;
                }

                _logger.LogInformation("Route retrieved successfully from repository. Query duration: {Duration}ms, RouteId: {RouteId}", 
                    stopwatch.ElapsedMilliseconds,
                    route.Id);

                return route;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Route retrieval operation was cancelled after {Duration}ms", 
                    stopwatch.ElapsedMilliseconds);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving route from repository. Duration: {Duration}ms", 
                    stopwatch.ElapsedMilliseconds);
                throw;
            }
        }
    }

    public async Task SaveRouteAsync(RouteResult route, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = route.CorrelationId,
            ["RouteId"] = route.Id
        }))
        {
            try
            {
                _logger.LogInformation("Starting route save operation");

                await _repository.SaveRouteAsync(route, ct);

                stopwatch.Stop();

                _logger.LogInformation("Route saved successfully to repository. Duration: {Duration}ms", 
                    stopwatch.ElapsedMilliseconds);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Route save operation was cancelled after {Duration}ms", 
                    stopwatch.ElapsedMilliseconds);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while saving route to repository. Duration: {Duration}ms", 
                    stopwatch.ElapsedMilliseconds);
                throw;
            }
        }
    }
}