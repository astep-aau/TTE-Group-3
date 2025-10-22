using Microsoft.EntityFrameworkCore;
using translator_service.Domain.Entities;

namespace translator_service.Features.GetTravelTime;

public class GetTravelTimeHandler
{
    private readonly TranslatorDbContext _context;
    private readonly ILogger<GetTravelTimeHandler> _logger;

    public GetTravelTimeHandler(TranslatorDbContext context, ILogger<GetTravelTimeHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SaveTravelTimeAsync(TravelTimeResult travelTime, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Saving travel time for CorrelationId={CorrelationId}", travelTime.CorrelationId);

            var existing = await _context.TravelTimeResults
                .FirstOrDefaultAsync(x => x.CorrelationId == travelTime.CorrelationId, cancellationToken);

            if (existing is null)
            {
                await _context.TravelTimeResults.AddAsync(travelTime, cancellationToken);
                _logger.LogInformation("Added new travel time for CorrelationId={CorrelationId}", travelTime.CorrelationId);
            }
            else
            {
                existing.TravelTimeMinutes = travelTime.TravelTimeMinutes;
                existing.CreatedAt = DateTime.UtcNow;
                _logger.LogInformation("Updated existing travel time for CorrelationId={CorrelationId}", travelTime.CorrelationId);
            }

            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Travel time saved successfully for CorrelationId={CorrelationId}", travelTime.CorrelationId);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error while saving travel time for CorrelationId={CorrelationId}", travelTime.CorrelationId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while saving travel time for CorrelationId={CorrelationId}", travelTime.CorrelationId);
            throw;
        }
    }

    public async Task<TravelTimeResult?> GetByCorrelationIdAsync(Guid correlationId, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Retrieving travel time for CorrelationId={CorrelationId}", correlationId);

            var result = await _context.TravelTimeResults
                .FirstOrDefaultAsync(x => x.CorrelationId == correlationId, cancellationToken);

            if (result is null)
            {
                _logger.LogDebug("No travel time found for CorrelationId={CorrelationId}", correlationId);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving travel time for CorrelationId={CorrelationId}", correlationId);
            throw;
        }
    }

    public async Task MarkAsDeliveredAsync(Guid correlationId, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Marking travel time as delivered for CorrelationId={CorrelationId}", correlationId);

            var entity = await _context.TravelTimeResults
                .FirstOrDefaultAsync(x => x.CorrelationId == correlationId, cancellationToken);

            if (entity is not null)
            {
                entity.IsDelivered = true;
                await _context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Travel time marked as delivered for CorrelationId={CorrelationId}", correlationId);
            }
            else
            {
                _logger.LogWarning("Cannot mark as delivered - entity not found for CorrelationId={CorrelationId}", correlationId);
            }
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error while marking travel time as delivered for CorrelationId={CorrelationId}", correlationId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking travel time as delivered for CorrelationId={CorrelationId}", correlationId);
            throw;
        }
    }
}