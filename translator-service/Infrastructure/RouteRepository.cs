using Microsoft.EntityFrameworkCore;
using translator_service.Domain.Entities;

namespace translator_service.Features.GetRoute;

public class RouteRepository : IRouteRepository
{
    private readonly TranslatorDbContext _context;

    public RouteRepository(TranslatorDbContext context)
    {
        _context = context;
    }

    public async Task<RouteResult?> GetRouteAsync(Guid correlationId, CancellationToken ct)
    {
        return await _context.Routes
            .Include(r => r.Path)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.CorrelationId == correlationId, ct);
    }

    public async Task SaveRouteAsync(RouteResult route, CancellationToken ct)
    {
        _context.Routes.Add(route);
        await _context.SaveChangesAsync(ct);
    }
}