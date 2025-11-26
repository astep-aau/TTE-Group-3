namespace StateService.Features.ProcessFinished
{
    // Message published when UI returns the final result and process should be marked finished
    public record ProcessFinishedMessage(int Pid, string ResultSummary, string? CorrelationId);
}
