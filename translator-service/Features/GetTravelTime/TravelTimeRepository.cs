using Microsoft.EntityFrameworkCore;
using translator_service.Domain.Entities;

namespace translator_service.Infrastructure
{
    public class TravelTimeRepository : ITravelTimeRepository
    {
        private readonly TranslatorDbContext _context;

        public TravelTimeRepository(TranslatorDbContext context)
        {
            _context = context;
        }

        public async Task<TravelTimeResult?> GetTravelTimeByCorrelationIdAsync(Guid correlationId, CancellationToken ct)
        {
            return await _context.Set<TravelTimeResult>()
                .FirstOrDefaultAsync(x => x.CorrelationId == correlationId, ct);
        }

        public async Task UpdateTravelTimeAsync(TravelTimeResult travelTime, CancellationToken ct)
        {
            _context.Set<TravelTimeResult>().Update(travelTime);
            await _context.SaveChangesAsync(ct);
        }
    }
}