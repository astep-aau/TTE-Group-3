using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using FluentResults;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RouteEstimationService.Domain.Entities;
using RouteEstimationService.Features.CreateRoute;

namespace RouteEstimationService.Features.EstimateTime;

public class EstimateTimeHandler
{
    // Cache JsonSerializerOptions to address CA1869
    private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<CreateRouteHandler> _logger;
    private readonly string _pythonServiceBaseUrl;

    public EstimateTimeHandler(ILogger<CreateRouteHandler> logger, IConfiguration configuration)
    {
        _logger = logger;
        _pythonServiceBaseUrl = configuration.GetSection("PythonService")["BaseUrl"] ?? "http://localhost:8000";
        _logger.LogInformation("[EstimateTimeHandler] Configured PythonService BaseUrl: {BaseUrl}", _pythonServiceBaseUrl);
    }
    
    public Result<RouteResult> EstimateTime(RouteResult route)
    {
        if (route == null) return Result.Fail<RouteResult>("Route cannot be null");

        _logger.LogInformation("[EstimateTimeHandler] Estimating time for RouteId={RouteId} with EdgeIds=[{EdgeIds}]",
            route.RouteId, route.EdgeIds);

        using var http = new HttpClient();
        HttpResponseMessage response;
        try
        {
            string payload = JsonSerializer.Serialize(route.EdgeIds, _jsonOptions);
            response = http.PostAsync(
                $"{_pythonServiceBaseUrl}/Python/vectors",
                new StringContent(payload, Encoding.UTF8, "application/json")
            ).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EstimateTimeHandler] Failed to call embedding service at {BaseUrl}", _pythonServiceBaseUrl);
            return Result.Fail<RouteResult>($"Failed to call embedding service: {ex.Message}");
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("[EstimateTimeHandler] Embedding service returned non-success status code: {StatusCode}",
                response.StatusCode);
            return Result.Fail<RouteResult>($"Embedding service returned non-success status code: {response.StatusCode}");
        }

        string embeddedEdges;
        try
        {
            embeddedEdges = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EstimateTimeHandler] Failed to read response content");
            return Result.Fail<RouteResult>($"Failed to read response content: {ex.Message}");
        }
        _logger.LogInformation("[EstimateTimeHandler] Successfully received embedded edges for RouteId={RouteId}",
            route.RouteId);
        
        // Post embedded edges to time prediction service
        _logger.LogInformation("[EstimateTimeHandler] Posting embedded edges to time prediction service for RouteId={RouteId}",
            route.RouteId);
        try
        {
            response = http.PostAsync(
                $"{_pythonServiceBaseUrl}/Python/predict-time",
                new StringContent(embeddedEdges, Encoding.UTF8, "application/json")
            ).GetAwaiter().GetResult();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[EstimateTimeHandler] Failed to estimate time for RouteId={RouteId}", route.RouteId);
            return Result.Fail<RouteResult>($"Failed to estimate time for RouteId={route.RouteId}: {e.Message}");
        }
        
        string json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        using var doc = JsonDocument.Parse(json);
        route.EstimatedTimeSeconds = doc.RootElement.GetProperty("predicted_time").GetDouble();
        
        _logger.LogInformation(
            "[EstimateTimeHandler] Estimated time for RouteId={RouteId} is {EstimatedTimeSeconds} seconds",
            route.RouteId, route.EstimatedTimeSeconds);
        
        return Result.Ok(route);
    }
}