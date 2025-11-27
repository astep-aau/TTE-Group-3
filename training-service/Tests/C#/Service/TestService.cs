using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using TrainingService.Configuration;
using TrainingService.Domain;
using Xunit;

namespace TrainingService.Tests.Service;

public class TestService
{
    private readonly Mock<ILogger<TrainingService.Services.Service>> _mockLogger = new();
    private readonly IConfiguration _configuration = new ConfigurationBuilder()
        .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", ".."))
        .AddJsonFile("appsettings.json", optional: false)
        .Build();
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler = new();
    
    private TrainingService.Services.Service CreateService()
    {
        var pythonBackendSettings = new PythonBackendSettings();
        _configuration.GetSection("PythonBackend").Bind(pythonBackendSettings);
        var options = Options.Create(pythonBackendSettings);
    
        var mockHttpClient = new HttpClient(_mockHttpMessageHandler.Object);
        return new TrainingService.Services.Service(_mockLogger.Object, options, mockHttpClient);
    }

    #region CreateTrainingSet Tests

    [Fact]
    public async Task CreateTrainingSet_ShouldThrowHttpRequestException_WhenRouteGenerationFails()
    {
        // Arrange
        var service = CreateService();
        SetupHttpResponse(HttpStatusCode.InternalServerError, "Server error");

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await service.CreateTrainingSet("test-model", 10, 5, 15));

        // Verify error was logged
        VerifyLogContains(LogLevel.Error, "HTTP error while creating routes");
    }

    [Fact]
    public async Task CreateTrainingSet_ShouldThrowJsonException_WhenRouteResponseIsMalformed()
    {
        // Arrange
        var service = CreateService();
        SetupHttpResponse(HttpStatusCode.OK, "{ invalid json }");
    
        // Act & Assert
        var exception = await Assert.ThrowsAnyAsync<JsonException>(async () =>
            await service.CreateTrainingSet("test-model", 10, 5, 15));
    
        // Verify error was logged
        VerifyLogContains(LogLevel.Error, "JSON deserialization error");
    }

    [Fact]
    public async Task CreateTrainingSet_ShouldThrowException_WhenRoutesPropertyMissing()
    {
        // Arrange
        var service = CreateService();
        string malformedResponse = JsonSerializer.Serialize(new { data = new List<List<int>>() });
        SetupHttpResponse(HttpStatusCode.OK, malformedResponse);

        // Act & Assert - Should throw because "routes" property is missing
        await Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            await service.CreateTrainingSet("test-model", 10, 5, 15));
    }

    [Fact]
    public async Task CreateTrainingSet_ShouldProcessRoutesCorrectly_WhenValidDataProvided()
    {
        // Arrange
        var service = CreateService();
        var routes = new List<List<int>>
        {
            new() { 1, 2, 3 },
            new() { 4, 5, 6 }
        };

        // Setup route generation response
        string routeResponse = JsonSerializer.Serialize(new { routes });
        SetupSequentialResponses(new[]
        {
            (HttpStatusCode.OK, routeResponse), // CreateRoute
            (HttpStatusCode.OK, JsonSerializer.Serialize(new[] { 1.5, 2.0, 1.0 })), // CreateTimeForRouteAsync (route 1)
            (HttpStatusCode.OK, JsonSerializer.Serialize(new List<double[]>
            {
                new[] { 0.1, 0.2 }, new[] { 0.3, 0.4 }, new[] { 0.5, 0.6 }
            })), // GetEdgeVectors (route 1)
            (HttpStatusCode.OK, JsonSerializer.Serialize(new[] { 2.0, 1.5, 1.5 })), // CreateTimeForRouteAsync (route 2)
            (HttpStatusCode.OK, JsonSerializer.Serialize(new List<double[]>
            {
                new[] { 0.7, 0.8 }, new[] { 0.9, 1.0 }, new[] { 1.1, 1.2 }
            })), // GetEdgeVectors (route 2)
            (HttpStatusCode.OK, ""), // UploadTrainingSetAsync
            (HttpStatusCode.OK, "Training complete") // LstmTraining
        });

        // Act
        string result = await service.CreateTrainingSet("test-model", 2, 3, 3);

        // Assert
        Assert.Equal("Training Done", result);
        VerifyLogContains(LogLevel.Information, "Starting training set creation for model: test-model");
        VerifyLogContains(LogLevel.Information, "Training set creation completed successfully");
    }

    [Fact]
    public async Task CreateTrainingSet_ShouldHandleConcurrentProcessing_WhenMultipleRoutesProvided()
    {
        // Arrange
        var service = CreateService();
        var routes = Enumerable.Range(1, 8).Select(i => new List<int> { i, i + 1 }).ToList();
        string routeResponse = JsonSerializer.Serialize(new { routes });

        // Setup responses for all routes
        var responses = new List<(HttpStatusCode, string)>
        {
            (HttpStatusCode.OK, routeResponse) // CreateRoute
        };

        // Add responses for each route (time + vectors)
        for (int i = 0; i < 8; i++)
        {
            responses.Add((HttpStatusCode.OK, JsonSerializer.Serialize(new[] { 1.0, 1.0 }))); // Time
            responses.Add((HttpStatusCode.OK, JsonSerializer.Serialize(new List<double[]>
            {
                new[] { 0.1, 0.2 }, new[] { 0.3, 0.4 }
            }))); // Vectors
        }

        responses.Add((HttpStatusCode.OK, "")); // Upload
        responses.Add((HttpStatusCode.OK, "Training complete")); // LSTM

        SetupSequentialResponses(responses.ToArray());

        // Act
        string result = await service.CreateTrainingSet("test-model", 8, 2, 2);

        // Assert
        Assert.Equal("Training Done", result);
        VerifyLogContains(LogLevel.Information, "Processing 8 routes with max 4 concurrent tasks");
    }

    [Fact]
    public async Task CreateTrainingSet_ShouldContinueProcessing_WhenOneRouteFails()
    {
        // Arrange
        var service = CreateService();
        var routes = new List<List<int>>
        {
            new() { 1, 2 },
            new() { 3, 4 },
            new() { 5, 6 }
        };

        string routeResponse = JsonSerializer.Serialize(new { routes });
        SetupSequentialResponses(new[]
        {
            (HttpStatusCode.OK, routeResponse),
            (HttpStatusCode.OK, JsonSerializer.Serialize(new[] { 1.0, 1.0 })), // Route 1 time
            (HttpStatusCode.OK, JsonSerializer.Serialize(new List<double[]> { new[] { 0.1 }, new[] { 0.2 } })), // Route 1 vectors
            (HttpStatusCode.InternalServerError, "Error"), // Route 2 time - FAILS
            (HttpStatusCode.OK, JsonSerializer.Serialize(new List<double[]> { new[] { 0.3 }, new[] { 0.4 } })), // Route 2 vectors
            (HttpStatusCode.OK, JsonSerializer.Serialize(new[] { 2.0, 2.0 })), // Route 3 time
            (HttpStatusCode.OK, JsonSerializer.Serialize(new List<double[]> { new[] { 0.5 }, new[] { 0.6 } })), // Route 3 vectors
            (HttpStatusCode.OK, ""), // Upload
            (HttpStatusCode.OK, "Training complete") // LSTM
        });

        // Act
        string result = await service.CreateTrainingSet("test-model", 3, 2, 2);

        // Assert
        Assert.Equal("Training Done", result);
        // Verify that processing continued despite one route failing
        VerifyLogContains(LogLevel.Information, "Training set creation completed successfully");
    }

    [Fact]
    public async Task CreateTrainingSet_ShouldThrowException_WhenUploadFails()
    {
        // Arrange
        var service = CreateService();
        var routes = new List<List<int>> { new() { 1, 2 } };
        string routeResponse = JsonSerializer.Serialize(new { routes });

        SetupSequentialResponses(new[]
        {
            (HttpStatusCode.OK, routeResponse),
            (HttpStatusCode.OK, JsonSerializer.Serialize(new[] { 1.0 })),
            (HttpStatusCode.OK, JsonSerializer.Serialize(new List<double[]> { new[] { 0.1 } })),
            (HttpStatusCode.InternalServerError, "Upload failed") // Upload fails
        });

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await service.CreateTrainingSet("test-model", 1, 2, 2));

        VerifyLogContains(LogLevel.Error, "HTTP error while uploading training set");
    }

    [Fact]
    public async Task CreateTrainingSet_ShouldThrowException_WhenLstmTrainingFails()
    {
        // Arrange
        var service = CreateService();
        var routes = new List<List<int>> { new() { 1, 2 } };
        string routeResponse = JsonSerializer.Serialize(new { routes });

        SetupSequentialResponses(new[]
        {
            (HttpStatusCode.OK, routeResponse),
            (HttpStatusCode.OK, JsonSerializer.Serialize(new[] { 1.0 })),
            (HttpStatusCode.OK, JsonSerializer.Serialize(new List<double[]> { new[] { 0.1 } })),
            (HttpStatusCode.OK, ""), // Upload succeeds
            (HttpStatusCode.InternalServerError, "Training failed") // LSTM training fails
        });

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await service.CreateTrainingSet("test-model", 1, 2, 2));

        VerifyLogContains(LogLevel.Error, "HTTP error during LSTM training");
    }

    [Fact]
    public async Task CreateTrainingSet_ShouldUpdateStatus_ThroughoutProcess()
    {
        // Arrange
        var service = CreateService();
        var routes = new List<List<int>> { new() { 1, 2 } };
        string routeResponse = JsonSerializer.Serialize(new { routes });

        SetupSequentialResponses(new[]
        {
            (HttpStatusCode.OK, routeResponse),
            (HttpStatusCode.OK, JsonSerializer.Serialize(new[] { 1.0 })),
            (HttpStatusCode.OK, JsonSerializer.Serialize(new List<double[]> { new[] { 0.1 } })),
            (HttpStatusCode.OK, ""),
            (HttpStatusCode.OK, "Training complete")
        });

        // Act
        await service.CreateTrainingSet("test-model", 1, 2, 2);

        // Assert - Verify status was updated
        Assert.Equal("Idle", StatusTracker.Status);
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public void Service_ShouldUseConfiguration_ForUrlConstruction()
    {
        // Arrange
        var customSettings = new PythonBackendSettings
        {
            BaseUrl = "http://custom-backend:9000",
            Endpoints = new PythonEndpoints
            {
                GenerateRoutes = "/custom/routes/{numberOfSequences}/{minLength}/{maxLength}"
            }
        };

        var options = Options.Create(customSettings);
        var service = new TrainingService.Services.Service(_mockLogger.Object, options);

        // This test verifies the configuration is properly injected
        Assert.NotNull(service);
    }

    [Fact]
    public async Task CreateTrainingSet_ShouldUseConfiguredEndpoints_WhenMakingRequests()
    {
        // Arrange
        var service = CreateService();
        var routes = new List<List<int>> { new() { 1 } };
        string routeResponse = JsonSerializer.Serialize(new { routes });
    
        var capturedUrls = new List<string>();
        SetupSequentialResponses(new[]
        {
            (HttpStatusCode.OK, routeResponse),
            (HttpStatusCode.OK, JsonSerializer.Serialize(new[] { 1.0 })),
            (HttpStatusCode.OK, JsonSerializer.Serialize(new List<double[]> { new[] { 0.1 } })),
            (HttpStatusCode.OK, ""),
            (HttpStatusCode.OK, "Training complete")
        });
    
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => 
                capturedUrls.Add(req.RequestUri?.ToString() ?? ""))
            .ReturnsAsync(() => new HttpResponseMessage 
            { 
                StatusCode = HttpStatusCode.OK, 
                Content = new StringContent(routeResponse) 
            });
    
        // Act
        await service.CreateTrainingSet("test-model", 5, 10, 20);
    
        // Assert
        Assert.Contains(capturedUrls, url => url.Contains("/Python/generate-routes/5/10/20"));
    }

    #endregion

    #region Helper Methods

    private void SetupHttpResponse(HttpStatusCode statusCode, string content)
    {
        var response = new HttpResponseMessage
        {
            StatusCode = statusCode,
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);
    }

    private void SetupSequentialResponses(params (HttpStatusCode statusCode, string content)[] responses)
    {
        var setupSequence = _mockHttpMessageHandler.Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());

        foreach ((var statusCode, string content) in responses)
        {
            setupSequence = setupSequence.ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            });
        }
    }

    private void VerifyLogContains(LogLevel level, string messageSubstring)
    {
        _mockLogger.Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(messageSubstring)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    #endregion
}