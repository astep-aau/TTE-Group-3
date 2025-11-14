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
        
        [HttpPost("/Training/{modelName}")]
        public async Task<IActionResult> StartTraining(string modelName){
            try{
                var trainingSet = await _trainingService.CreateTrainingSet(modelName);
                return Ok(trainingSet);
            }catch (Exception ex){
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            return Ok(new { StatusTracker.Status });
        }
    }
}