using System.Diagnostics;
using translator_service.Domain.Entities;
using translator_service.Features.GetRoute;
using translator_service.Domain.Events;

namespace translator_service.Features.CreateProcess;

public class CreateProcessHandler
{
    private readonly ICreateProcessRepository _repository;
    private readonly ILogger<CreateProcessHandler> _logger;
    private readonly CreateProcessEmitter _emitter;

    public CreateProcessHandler(ICreateProcessRepository repository, 
        ILogger<CreateProcessHandler> logger,
        CreateProcessEmitter emitter)
    {
        _repository = repository;
        _logger = logger;
        _emitter = emitter;
    }
    
    public async Task HandleAsync(CreateProcessCommand command, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = command.CorrelationId,
            ["Origin"] = command.Origin,
            ["Destination"] = command.Destination,
            ["CreatedAt"] = command.CreatedAt
        }))
        {
            try
            {
                _logger.LogInformation("Starting process creation");

                var entity = new CreateProcessRequest
                {
                    Id = command.Id,
                    CorrelationId = command.CorrelationId,
                    Origin = command.Origin,
                    Destination = command.Destination,
                    CreatedAt = command.CreatedAt,
                    ModelVersion = command.ModelVersion,
                    TimeOfTravel = command.TimeOfTravel,
                    routeCreated = command.routeCreated,
                    timeEstimated = command.timeEstimated
                };
                
                // Alt andet er logging. This linje er den vigtige
                await _repository.CreateProcessAsync(entity);
                stopwatch.Stop();
                
                var processEvent = CreateProcessEvent.From(entity);
                await _emitter.EmitCreateProcessEventAsync(processEvent, ct);
                /*
                _logger.LogInformation("Process created successfully. Duration: {Duration}ms", 
                    stopwatch.ElapsedMilliseconds);
                */
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