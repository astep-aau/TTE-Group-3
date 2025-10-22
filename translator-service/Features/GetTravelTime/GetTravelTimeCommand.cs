using translator_service.Domain.Entities;

namespace translator_service.Features.GetTravelTime
{
    public class GetTravelTimeCommand
    {
        public Guid CorrelationId { get; }

        public GetTravelTimeCommand(TravelTimeRequest request)
        {
            CorrelationId = request.CorrelationId;
        }
    }
}

