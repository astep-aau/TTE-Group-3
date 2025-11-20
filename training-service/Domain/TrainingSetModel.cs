namespace trainingService.Domain;

public class Sequence
{
    public List<double[]> Edges { get; set; } = new List<double[]>();
    public double TotalTime { get; set; }
    public int TimeBucket { get; set; }
    public int DayOfWeek { get; set; }
}

public class TrainingSet
{
    public List<Sequence> Sequences { get; set; } = new List<Sequence>();
}