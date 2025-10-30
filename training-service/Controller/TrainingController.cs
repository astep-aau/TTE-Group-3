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
        
        [HttpPost("train")]
        public IActionResult StartTraining()
        {
            string result = _trainingService.CreateTrainingSet(); 
            return Ok(result);
        }

        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            return Ok(new { StatusTracker.Status });
        }

        [HttpPost("vector-embedding")]
        public IActionResult GetVectorEmbedding()
        {
            _vectorEmbeddingService.VectorEmbedding();
            return Ok("Done");
        }
    }
}