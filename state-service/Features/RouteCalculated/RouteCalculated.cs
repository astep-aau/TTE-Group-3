namespace StateService.Features.RouteCalculated
{
    // Message published after the route finding service calculates a route
    public record RouteCalculatedMessage(int Pid, string RouteJson, string? CorrelationId);
}
