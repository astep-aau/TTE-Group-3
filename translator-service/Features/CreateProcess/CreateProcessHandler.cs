using System.Diagnostics;
using translator_service.Domain.Entities;
using translator_service.Features.GetRoute;

namespace translator_service.Features.CreateProcess;

public class CreateProcessHandler
{
    private readonly ICreateProcessRepository _repository;
    private readonly ILogger<CreateProcessHandler> _logger;

    public CreateProcessHandler(ICreateProcessRepository repository, ILogger<CreateProcessHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }
    
    public async Task HandleAsync(CreateProcessRequest request, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = request.CorrelationId,
            ["Origin"] = request.Origin,
            ["Destination"] = request.Destination,
            ["CreatedAt"] = request.CreatedAt
        }))
        {
            try
            {
                _logger.LogInformation("Starting process creation");

                // Alt andet er logging. This linje er den vigtige
                await _repository.CreateProcessAsync(request);

                stopwatch.Stop();

                _logger.LogInformation("Process created successfully. Duration: {Duration}ms", 
                    stopwatch.ElapsedMilliseconds);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Process creation operation was cancelled after {Duration}ms", 
                    stopwatch.ElapsedMilliseconds);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating process. Duration: {Duration}ms", 
                    stopwatch.ElapsedMilliseconds);
                throw;
            }
        }
    }
    
}