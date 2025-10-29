using Microsoft.AspNetCore.Mvc;
using trainingService.Domain;
using TrainingService.Services;

namespace TrainingService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TrainingController : ControllerBase
    {
        private readonly TrainingService.Services.TrainingService _trainingService;
        private readonly VectorEmbeddingService.Services.VectorEmbeddingService _vectorEmbeddingService;
        
        public TrainingController(Services.TrainingService trainingService,  VectorEmbeddingService.Services.VectorEmbeddingService vectorEmbeddingService)
        {
            _trainingService = trainingService;
            _vectorEmbeddingService = vectorEmbeddingService;
        }
        
        [HttpGet("train")]
        public IActionResult StartTraining()
        {
            TrainingSet result = _trainingService.CreateTrainingSet(); 
            return Ok(result);
        }

        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            // Placeholder for job status
            return Ok(new { Status = "Idle" });
        }

        [HttpGet("vector-embedding")]
        public IActionResult GetVectorEmbedding()
        {
            var result = _vectorEmbeddingService.VectorEmbedding();
            return Ok(result);
        }
    }
}