using TrainingService.Domain;

namespace TrainingService.Infrastructure;

public interface ITrainingQueue
{
    void Enqueue(TrainingRequest job);
    TrainingRequest? Dequeue();
}