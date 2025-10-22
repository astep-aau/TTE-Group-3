using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Dapper;

namespace translator_service.Endpoints;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly ILogger<HealthController> _logger;

    public HealthController(IConfiguration config, ILogger<HealthController> logger)
    {
        _config = config;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetHealth(CancellationToken ct)
    {
        string dbStatus;
        string dbMessage;

        try
        {
            var connectionString = _config.GetConnectionString("DefaultConnection");

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(ct);

            await connection.ExecuteScalarAsync("SELECT 1", ct);

            dbStatus = "Healthy";
            dbMessage = "Database connection successful";
        }
        catch (Exception ex)
        {
            dbStatus = "Unhealthy";
            dbMessage = ex.Message;
            _logger.LogError(ex, "Database connection failed");
        }

        var response = new
        {
            service = "Route Service",
            status = dbStatus == "Healthy" ? "Healthy" : "Degraded",
            database = new
            {
                status = dbStatus,
                message = dbMessage
            },
            timestamp = DateTime.UtcNow
        };
        
        return dbStatus == "Healthy"
            ? Ok(response)
            : StatusCode(503, response);
    }
}