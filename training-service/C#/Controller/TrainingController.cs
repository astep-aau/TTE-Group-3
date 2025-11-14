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
        
        public TrainingController(Services.TrainingService trainingService)
        {
            _trainingService = trainingService;
        }

        [HttpPost("/Training/start-training")]
        public async Task<IActionResult> StartTraining([FromBody] TrainingRequest request){
            try{
                var trainingSet = await _trainingService.CreateTrainingSet(
                    request.ModelName, 
                    request.NumberOfRoutes, 
                    request.MinLength, 
                    request.MaxLength
                );
                    return Ok(trainingSet);
            }catch (Exception ex){
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("Training/status")]
        public IActionResult GetStatus()
        {
            return Ok(new { StatusTracker.Status });
        }
    }
}