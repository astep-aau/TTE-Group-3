using System.Diagnostics;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace translator_service.API;

[ApiController]
[Route("api/logs/frontend")]

public class LoggingFromFrontendEndpoint : ControllerBase
{
    private readonly ILogger<LoggingFromFrontendEndpoint> _logger;
    
    public LoggingFromFrontendEndpoint(ILogger<LoggingFromFrontendEndpoint> logger)
    {
        _logger = logger;
    }

    [HttpPost]
    public IActionResult LogError([FromBody] FrontendError errorLog)
    {
        _logger.LogInformation("Received frontend error log");
        _logger.LogError("[Frontend Error] {message} | URL: {url} | Error: {error}",
            errorLog.Message,
            errorLog.Url,
            errorLog.Error?.Message
            );
        _logger.LogInformation("Logging completed");
        return Ok();
    }

    public class FrontendError
    {
        [JsonPropertyName("message")]
        public string Message { get; set; }
        [JsonPropertyName("url")]
        public string Url { get; set; }
        [JsonPropertyName("error")]
        public ErrorDetails Error { get; set; }
    }

    public class ErrorDetails
    {
        [JsonPropertyName("message")]
        public string Message { get; set; }
    }
}


