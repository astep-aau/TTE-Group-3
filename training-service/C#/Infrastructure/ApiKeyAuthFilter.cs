using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TrainingService.Infrastructure;

public class ApiKeyAuthFilter : IAuthorizationFilter
{
    private const string ApiKeyHeaderName = "X-API-Key";
    private readonly IConfiguration _configuration;
    private readonly ILogger<ApiKeyAuthFilter> _logger;

    public ApiKeyAuthFilter(IConfiguration configuration, ILogger<ApiKeyAuthFilter> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue(ApiKeyHeaderName, out var extractedApiKey))
        {
            _logger.LogWarning("API key missing from request");
            context.Result = new UnauthorizedObjectResult("API key is missing");
            return;
        }

        string? apiKey = _configuration["ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogError("API key not configured in appsettings. Configuration value: {ConfigValue}", 
                apiKey ?? "null");
            context.Result = new StatusCodeResult(StatusCodes.Status500InternalServerError);
            return;
        }

        if (apiKey.Equals(extractedApiKey)) return;
        _logger.LogWarning("Invalid API key provided");
        context.Result = new UnauthorizedObjectResult("Invalid API key");
    }
}