namespace StateService.Features.TimeEstimated
{
    // Message published when time estimation service finishes estimating the travel time
    public record TimeEstimatedMessage(int Pid, int Seconds, string ModelVersion, string? CorrelationId);
}
