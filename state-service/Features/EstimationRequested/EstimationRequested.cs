namespace StateService.Features.EstimationRequested
{
    // Message published when estimation is requested (start of process)
    public record EstimationRequestedMessage(int Pid, string Start, string End, DateTime TravelTimeUtc, string? CorrelationId);
}
