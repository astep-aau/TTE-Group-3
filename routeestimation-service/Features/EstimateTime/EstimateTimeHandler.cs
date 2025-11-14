using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using FluentResults;
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

    private readonly ILogger<CreateRouteConsumer> _logger;

    public EstimateTimeHandler(ILogger<CreateRouteConsumer> logger)
    {
        _logger = logger;
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
                "http://127.0.0.1:8000/Python/vectors",
                new StringContent(payload, Encoding.UTF8, "application/json")
            ).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EstimateTimeHandler] Failed to call embedding service");
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

        // Parse and ddd embeddedEdges to route
        List<List<double>> parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<List<List<double>>>(embeddedEdges, _jsonOptions)
                     ?? JsonSerializer.Deserialize<List<List<double>>>(JsonSerializer.Deserialize<string>(embeddedEdges)!, _jsonOptions)
                     ?? throw new JsonException("Unable to parse embeddings");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EstimateTimeHandler] Failed to parse embedded edges JSON");
            return Result.Fail<RouteResult>($"Failed to parse embedded edges JSON: {ex.Message}");
        }
        
        route.EmbeddedEdges = parsed;
        
        // Post embedded edges to time prediction service
        _logger.LogInformation("[EstimateTimeHandler] Posting embedded edges to time prediction service for RouteId={RouteId}",
            route.RouteId);
        try
        {
            response = http.PostAsync(
                "http://127.0.0.1:8000/Python/predict-time",
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