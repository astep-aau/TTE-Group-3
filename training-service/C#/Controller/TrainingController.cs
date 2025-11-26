using Microsoft.AspNetCore.Mvc;
using TrainingService.Domain;   // For TrainingRequest DTO
using TrainingService.Infrastructure;

namespace TrainingService.Controllers
{
    /// <summary>
    /// Controller responsible for managing machine learning model training requests.
    /// </summary>
    [ApiController]
    [Route("[controller]")]
    public class TrainingController : ControllerBase
    {
        private readonly ITrainingQueue _queue;
        private readonly ILogger<TrainingController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="TrainingController"/> class.
        /// </summary>
        /// <param name="queue">The training queue for managing background training tasks.</param>
        /// <param name="logger">The logger instance for diagnostic logging.</param>
        public TrainingController(ITrainingQueue queue, ILogger<TrainingController> logger)
        {
            _queue = queue;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves the current status of the training process.
        /// </summary>
        /// <returns>
        /// An <see cref="IActionResult"/> containing the current training status.
        /// </returns>
        /// <response code="200">200 OK - Returns the current training status.</response>
        [HttpGet("Training/status")]
        public IActionResult GetStatus()
        {
            _logger.LogDebug("[C# Controller]: Status check requested");

            string status = StatusTracker.Status;
            _logger.LogInformation("[C# Controller]: Current training status: {Status}", status);

            return Ok(new { StatusTracker.Status });
        }

        /// <summary>
        /// Initiates a background training task for the specified model.
        /// </summary>
        /// <param name="request">The training request containing model configuration.</param>
        /// <returns>
        /// An <see cref="IActionResult"/> indicating whether the training was successfully queued.
        /// <para>Response codes:</para>
        /// <list type="bullet">
        /// <item><description>200 OK - Training request successfully queued.</description></item>
        /// <item><description>400 Bad Request - Invalid request parameters.</description></item>
        /// <item><description>401 Unauthorized - Missing or invalid API key.</description></item>
        /// <item><description>500 Internal Server Error - Failed to queue the training request.</description></item>
        /// </list>
        /// </returns>
        /// <remarks>
        /// <para>Sample request:</para>
        /// <code>
        /// POST /Training/start-training
        /// Headers: X-API-Key: your-secret-key
        /// {
        ///    "modelName": "LSTM",
        ///    "numberOfRoutes": 1000,
        ///    "minLength": 5,
        ///    "maxLength": 20
        /// }
        /// </code>
        /// </remarks>
        [HttpPost("/Training/start-training")]
        [ServiceFilter(typeof(ApiKeyAuthFilter))]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public IActionResult StartTraining([FromBody] TrainingRequest request)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("[C# Controller]: Invalid training request received");
                return BadRequest(ModelState);
            }

            _logger.LogInformation("[C# Controller]: Training request received for model: {ModelName}", request.ModelName);

            try
            {
                _queue.Enqueue(request);
                _logger.LogInformation("[C# Controller]: Training request queued successfully for model: {ModelName}", request.ModelName);

                return Ok(new
                {
                    Status = "Queued",
                    Message = "Training started in background."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[C# Controller]: Failed to queue training request for model: {ModelName}", request.ModelName);
                return StatusCode(500, new { Status = "Failed", Message = "Failed to queue training request" });
            }
        }
    }
}