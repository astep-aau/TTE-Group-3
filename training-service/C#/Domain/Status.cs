namespace TrainingService.Domain;

public static class StatusTracker
{
    private static string _status = "Idle";
    private static readonly Lock Lock = new();
    
    public static string Status
    {
        get { lock (Lock) return _status; }
        set { lock (Lock) _status = value; }
    }
}