using Microsoft.AspNetCore.Mvc;
using TrainingService.Services; // For TrainingService class
using TrainingService.Domain;   // For TrainingRequest DTO
using TrainingService.Infrastructure;

namespace TrainingService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TrainingController : ControllerBase
    {
        private readonly Service _trainingService;
        private readonly TrainingQueue _queue;
        
        public TrainingController(Service trainingService, TrainingQueue queue)
        {
            _trainingService = trainingService;
            _queue = queue;
        }

        [HttpPost("/Training/start-training")]
        public IActionResult StartTraining([FromBody] TrainingRequest request)
        {
            _queue.Enqueue(request);

            return Ok(new 
            { 
                Status = "Queued", 
                Message = "Training started in background." 
            });
        }

        [HttpGet("Training/status")]
        public IActionResult GetStatus()
        {
            return Ok(new { StatusTracker.Status });
        }
    }
}