using System.ComponentModel.DataAnnotations;

namespace trainingService.Domain;
public class TrainingRequest
{
    [Required] public string ModelName { get; set; } = string.Empty;
    [Required] public int NumberOfRoutes { get; set; }
    [Required] public int MinLength { get; set; }
    [Required] public int MaxLength { get; set; }
}