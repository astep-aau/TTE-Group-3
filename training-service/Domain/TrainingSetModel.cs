namespace trainingService.Domain;

public class Sequence
{
    public List<double[]> Edges { get; set; }
    public double TotalTime { get; set; }
}

public class TrainingSet
{
    public List<Sequence> Sequences { get; set; }
}