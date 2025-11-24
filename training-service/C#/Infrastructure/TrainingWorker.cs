using TrainingService.Domain;
using TrainingService.Controllers;
using TrainingService.Services;

namespace TrainingService.Infrastructure;
public class TrainingWorker : BackgroundService
{
    private readonly TrainingQueue _queue;
    private readonly Service _trainingService;

    public TrainingWorker(TrainingQueue queue, Service trainingService)
    {
        _queue = queue;
        _trainingService = trainingService;
    }

    

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var job = _queue.Dequeue();
            if (job != null)
            {
                StatusTracker.Status = "Training in progress";

                try
                {
                    await _trainingService.CreateTrainingSet(
                        job.ModelName, job.NumberOfRoutes, job.MinLength, job.MaxLength);
                    StatusTracker.Status = "Completed";
                }
                catch (Exception ex)
                {
                    StatusTracker.Status = "Error: " + ex.Message;
                }
            }

            await Task.Delay(100);
        }
    }
}