using TrainingService.Domain;
using TrainingService.Controllers;

namespace TrainingService.Infrastructure;
public class TrainingQueue
{
    private readonly Queue<TrainingRequest> _jobs = new();
    private readonly object _lock = new();

    public void Enqueue(TrainingRequest job)
    {
        lock (_lock)
        {
            _jobs.Enqueue(job);
            StatusTracker.Status = "Queued job";
        }
    }

    public TrainingRequest? Dequeue()
    {
        lock (_lock)
        {
            return _jobs.Count > 0 ? _jobs.Dequeue() : null;
        }
    }
}