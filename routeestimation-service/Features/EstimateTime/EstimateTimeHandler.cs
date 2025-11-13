using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using FluentResults;
using RouteEstimationService.Domain.Entities;

namespace RouteEstimationService.Features.EstimateTime;

public class EstimateTimeHandler
{
    // Cache JsonSerializerOptions to address CA1869
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };
    
    public static Result<RouteResult> EstimateTime(RouteResult route)
    {
        if (route == null) return Result.Fail<RouteResult>("Route cannot be null");
        
        // Call the external Python script in the training service to get estimated time for the route
        using var http = new HttpClient();
        HttpResponseMessage response;
        try
        {
            string payload = JsonSerializer.Serialize(route.EdgeIds, JsonOptions);
            response = http.PostAsync(
                "http://127.0.0.1:8000/Python/predict-time",
                new StringContent(payload, Encoding.UTF8, "application/json")
            ).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            return Result.Fail<RouteResult>($"Failed to call embedding service: {ex.Message}");
        }

        if (!response.IsSuccessStatusCode)
            return Result.Fail<RouteResult>($"Embedding service returned non-success status code: {response.StatusCode}");

        string embeddedEdgesObj;
        try
        {
            embeddedEdgesObj = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            return Result.Fail<RouteResult>($"Failed to read response content: {ex.Message}");
        }

        // Add embeddedEdges to route
        try
        {
            var embeddedEdges = JsonSerializer.Deserialize<List<double>>(embeddedEdgesObj, JsonOptions);
            if (embeddedEdges == null)
                return Result.Fail<RouteResult>("Failed to deserialize embedded edges: result was null");

            route.EmbeddedEdges = embeddedEdges;
        }
        catch (Exception ex)
        {
            return Result.Fail<RouteResult>($"Failed to add embeddedEdges to route: {ex.Message}");
        }
        
        // Handle time estimation
        try
        {
            response = http.PostAsync(
                "http://127.0.0.1:8000/Python/predict-time",
                new StringContent(embeddedEdgesObj, Encoding.UTF8, "application/json")
            ).GetAwaiter().GetResult();
        }
        catch (Exception e)
        {
            return Result.Fail<RouteResult>($"Failed to estimate time for RouteId={route.RouteId}: {e.Message}");
        }
        
        route.EstimatedTimeSeconds = double.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());

        return Result.Ok(route);
    }
}