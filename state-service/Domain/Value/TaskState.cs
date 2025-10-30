namespace StateService.Domain.Value
{
    public enum TaskState
    {
        New = 0,
        RouteFinding = 1,
        TimeEstimation = 2,
        ModelLoading = 3,
        UiReturn = 4,
        Finished = 5
    }
}
