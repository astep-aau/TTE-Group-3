
// TODO: Create the emitter for the CreateProcess feature, skeleton because RabbitMQ is not yet implemented
using System.Threading.Tasks;
using System.Collections.Generic;
using MassTransit;
using translator_service.Domain.Events;

namespace translator_service.Features.CreateProcess
{
    public class CreateProcessEmitter
    {
        // Placeholder for future RabbitMQ bus implementation
        private readonly IBus _bus;

        public CreateProcessEmitter(IBus bus)
        {
            _bus = bus;
        }
        
        public async Task EmitCreateProcessEventAsync(CreateProcessEvent processEvent, CancellationToken ct = default)
        {
            await _bus.Publish(processEvent, ct);
        }
    }
}
