using translator_service.Domain.Entities;

namespace translator_service.Features.GetRoute;

public class CreateProcessRepository : ICreateProcessRepository
{
    private readonly TranslatorDbContext _context;

    public CreateProcessRepository(TranslatorDbContext context)
    {
        _context = context;
    }
    
    public async Task CreateProcessAsync(CreateProcessRequest request)
    {
        _context.CreateProcessRequests.Add(request);
        await _context.SaveChangesAsync();
    }
}