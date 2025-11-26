using translator_service.Domain.Entities;

namespace translator_service.Features.GetRoute;

public interface ICreateProcessRepository
{
    Task CreateProcessAsync(CreateProcessRequest request);
}