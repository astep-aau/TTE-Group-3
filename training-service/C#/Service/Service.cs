using System.Text.Json;
using TrainingService.Domain;
using System.Text;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.Options;
using TrainingService.Configuration;

namespace TrainingService.Services;

/// <summary>
/// Service responsible for orchestrating machine learning model training workflows.
/// Handles route generation, data processing, and communication with Python ML backend.
/// </summary>
public class Service
{
    private readonly HttpClient _client;
    private readonly ILogger<Service> _logger;
    private readonly PythonBackendSettings _pythonSettings;
    private readonly object _hashSetLock = new object();
    private readonly HashSet<Sequence> uniqueSequences = new HashSet<Sequence>();

    public Service(ILogger<Service> logger, IOptions<PythonBackendSettings> pythonSettings, HttpClient? httpClient = null)
    {
        _logger = logger;
        _pythonSettings = pythonSettings.Value;
        _client = httpClient ?? new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan,
            DefaultRequestVersion = HttpVersion.Version11,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact
        };
    }

    /// <summary>
    /// Generates random routes by calling the Python backend service.
    /// </summary>
    /// <param name="numberOfSequences">The number of routes to generate.</param>
    /// <param name="minLength">The minimum length of each route (number of edges).</param>
    /// <param name="maxLength">The maximum length of each route (number of edges).</param>
    /// <returns>A list of routes, where each route is a list of edge IDs.</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request to Python backend fails.</exception>
    /// <exception cref="JsonException">Thrown when the response cannot be deserialized.</exception>
    private async Task<List<List<int>>> CreateRoute(int numberOfSequences, int minLength, int maxLength)
    {
        _logger.LogInformation("[C# Service]: Creating {NumberOfSequences} routes with length between {MinLength} and {MaxLength}",
            numberOfSequences, minLength, maxLength);

        try
        {
            string endpoint = _pythonSettings.Endpoints.GenerateRoutes
                .Replace("{numberOfSequences}", numberOfSequences.ToString())
                .Replace("{minLength}", minLength.ToString())
                .Replace("{maxLength}", maxLength.ToString());
            var url = $"{_pythonSettings.BaseUrl}{endpoint}";
            
            _logger.LogDebug("[C# Service]: Sending GET request to {Url}", url);

            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMinutes(5));
            var httpResponse = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Get, url), HttpCompletionOption.ResponseContentRead, cts.Token);
            httpResponse.EnsureSuccessStatusCode();

            string responseJson = await httpResponse.Content.ReadAsStringAsync();
            var edgeSequences = JsonSerializer.Deserialize<List<List<int>>>(
                JsonDocument.Parse(responseJson).RootElement.GetProperty("routes").GetRawText())!;
            
            if (edgeSequences == null || edgeSequences.Count == 0)
            {
                var errorMessage = $"Python backend returned empty route list for {numberOfSequences} sequences";
                _logger.LogError("[C# Service]: {ErrorMessage}", errorMessage);
                StatusTracker.Status = "Error: " + errorMessage;
                throw new InvalidOperationException(errorMessage);
            }

            _logger.LogInformation("[C# Service]: Successfully created {RoutesCount} routes", edgeSequences.Count);
            return edgeSequences;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[C# Service]: HTTP error while creating routes: {StatusCode}", ex.StatusCode);
            StatusTracker.Status = "Error creating routes: " + ex.Message;
            throw;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "[C# Service]: JSON deserialization error while parsing routes");
            StatusTracker.Status = "Error parsing route response: " + ex.Message;
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[C# Service]: Unexpected error while creating routes (not HTTP/JSON related)");
            StatusTracker.Status = "Error creating routes: " + ex.Message;
            throw;
        }
    }

    /// <summary>
    /// Calculates the total time for a given route by calling the Python backend.
    /// </summary>
    /// <param name="edges">The list of edge IDs representing the route.</param>
    /// <returns>The total time required to traverse the route, or 0.0 if calculation fails.</returns>
    /// <remarks>
    /// Returns 0.0 if the edge list is null, empty, or if an error occurs during calculation.
    /// </remarks>
    private async Task<double> CreateTimeForRouteAsync(List<int>? edges)
    {
        if (edges == null || edges.Count == 0)
        {
            _logger.LogWarning("[C# Service]: Attempted to calculate time for null or empty edge list");
            return 0.0;
        }

        _logger.LogDebug("[C# Service]: Calculating time for route with {EdgeCount} edges", edges.Count);

        try
        {
            var url = $"{_pythonSettings.BaseUrl}{_pythonSettings.Endpoints.CalculateRouteTime}";
            string jsonBody = JsonSerializer.Serialize(edges);
            using var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMinutes(5));
            HttpResponseMessage response = await _client.PostAsync(url, content, cts.Token);
            response.EnsureSuccessStatusCode();

            string responseJson = await response.Content.ReadAsStringAsync();
            var times = JsonSerializer.Deserialize<List<double>>(responseJson);
            double totalTime = times?.Sum() ?? 0.0;

            _logger.LogDebug("[C# Service]: Calculated total time: {TotalTime} for {EdgeCount} edges", totalTime, edges.Count);
            return totalTime;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[C# Service]: HTTP error while calculating route time: {StatusCode}", ex.StatusCode);
            return 0.0;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "[C# Service]: JSON error while parsing time calculation response");
            return 0.0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[C# Service]: Unexpected error while calculating route time (not HTTP/JSON related)");
            return 0.0;
        }
    }

    /// <summary>
    /// Retrieves vector representations for a list of edges from the Python backend.
    /// </summary>
    /// <param name="edges">The list of edge IDs to convert to vectors.</param>
    /// <returns>A list of double arrays representing the edge vectors, or an empty list if retrieval fails.</returns>
    /// <remarks>
    /// Each edge is converted to a multidimensional vector representation used for ML model input.
    /// </remarks>
    private async Task<List<double[]>> GetEdgeVectors(List<int>? edges)
    {
        if (edges == null || edges.Count == 0)
        {
            _logger.LogWarning("[C# Service]: Attempted to get vectors for null or empty edge list");
            return new List<double[]>();
        }

        _logger.LogDebug("[C# Service]: Getting vectors for {EdgeCount} edges", edges.Count);

        try
        {
            var url = $"{_pythonSettings.BaseUrl}{_pythonSettings.Endpoints.Vectors}";
            string jsonBody = JsonSerializer.Serialize(edges);
            using var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMinutes(5));
            HttpResponseMessage response = await _client.PostAsync(url, content, cts.Token);
            response.EnsureSuccessStatusCode();

            string responseJson = await response.Content.ReadAsStringAsync();
            var vectors = JsonSerializer.Deserialize<List<double[]>>(responseJson);

            _logger.LogDebug("[C# Service]: Successfully retrieved {VectorCount} vectors", vectors?.Count ?? 0);
            return vectors ?? new List<double[]>();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[C# Service]: HTTP error while getting edge vectors: {StatusCode}", ex.StatusCode);
            return new List<double[]>();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "[C# Service]: Could not parse vector output");
            return new List<double[]>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[C# Service]: Unexpected error while getting edge vectors (not HTTP/JSON related)");
            return new List<double[]>();
        }
    }

    /// <summary>
    /// Initiates LSTM model training by calling the Python backend service.
    /// </summary>
    /// <param name="modelName">The name of the model to train.</param>
    /// <returns>A task representing the asynchronous training operation.</returns>
    /// <exception cref="HttpRequestException">Thrown when the training request fails.</exception>
    /// <remarks>
    /// This method triggers the training process on the Python backend after the training data has been uploaded.
    /// </remarks>
    private async Task LstmTraining(string modelName)
    {
        _logger.LogInformation("[C# Service]: Starting LSTM training for model: {ModelName}", modelName);
        StatusTracker.Status = "LSTM Training";
    
        try
        {
            string endpoint = _pythonSettings.Endpoints.TrainLstm.Replace("{modelName}", modelName);
            var url = $"{_pythonSettings.BaseUrl}{endpoint}";
    
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromHours(5));
            HttpResponseMessage response = await _client.PostAsync(url, null, cts.Token);
            response.EnsureSuccessStatusCode();
    
            string responseContent = await response.Content.ReadAsStringAsync(cts.Token);
            _logger.LogInformation("[C# Service]: LSTM training completed for model: {ModelName}. Response: {Response}",
                modelName, responseContent);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogError(ex, "[C# Service]: LSTM training timed out for model: {ModelName}", modelName);
            throw new TimeoutException($"LSTM training exceeded timeout for model: {modelName}", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[C# Service]: HTTP error during LSTM training for model: {ModelName}, Status: {StatusCode}",
                modelName, ex.StatusCode);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[C# Service]: Unexpected error during LSTM training for model (not HTTP related): {ModelName}", modelName);
            throw;
        }
    }

    /// <summary>
    /// Uploads the training dataset to the Python backend and initiates model training.
    /// </summary>
    /// <param name="trainingSet">The training set containing sequences and labels.</param>
    /// <param name="modelName">The name of the model to train with this dataset.</param>
    /// <returns>A task representing the asynchronous upload and training operation.</returns>
    /// <exception cref="HttpRequestException">Thrown when the upload request fails.</exception>
    private async Task UploadTrainingSetAsync(TrainingSet trainingSet, string modelName)
    {
        _logger.LogInformation("[C# Service]: Uploading training set with {SequenceCount} sequences for model: {ModelName}",
            trainingSet.Sequences.Count, modelName);
    
        try
        {
            byte[] fileBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(trainingSet));
            using var streamContent = new ByteArrayContent(fileBytes);
            using var form = new MultipartFormDataContent();
            form.Add(streamContent, "file", "TrainingSet.json");
    
            var url = $"{_pythonSettings.BaseUrl}{_pythonSettings.Endpoints.TrainingFile}";
            
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMinutes(10)); // Adjust based on expected upload duration
            HttpResponseMessage response = await _client.PostAsync(url, form, cts.Token);
            response.EnsureSuccessStatusCode();
    
            _logger.LogInformation("[C# Service]: Successfully uploaded training set for model: {ModelName}", modelName);
    
            await LstmTraining(modelName);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogError(ex, "[C# Service]: Training set upload timed out for model: {ModelName}", modelName);
            throw new TimeoutException($"Training set upload exceeded timeout for model: {modelName}", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[C# Service]: HTTP error while uploading training set: {StatusCode}", ex.StatusCode);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[C# Service]: Error uploading training set for model (not HTTP related): {ModelName}", modelName);
            throw;
        }
    }

    /// <summary>
    /// Creates a complete training dataset by generating routes, processing them, and initiating model training.
    /// </summary>
    /// <param name="modelName">The name of the model to train.</param>
    /// <param name="numberOfRoutes">The number of routes to generate for the training set.</param>
    /// <param name="minLength">The minimum route length (number of edges).</param>
    /// <param name="maxLength">The maximum route length (number of edges).</param>
    /// <returns>A message indicating training completion.</returns>
    /// <remarks>
    /// <para>This method orchestrates the entire training pipeline:</para>
    /// <list type="number">
    /// <item><description>Generates random routes</description></item>
    /// <item><description>Processes routes concurrently (max 4 parallel tasks)</description></item>
    /// <item><description>Calculates edge vectors and route times</description></item>
    /// <item><description>Uploads the training set</description></item>
    /// <item><description>Initiates LSTM training</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="HttpRequestException">Thrown when communication with the Python backend fails.</exception>
    /// <exception cref="JsonException">Thrown when data serialization/deserialization fails.</exception>
    public async Task<string> CreateTrainingSet(
        string modelName,
        int numberOfRoutes,
        int minLength,
        int maxLength)
    {
        _logger.LogInformation("[C# Service]: Starting training set creation for model: {ModelName} with {NumberOfRoutes} routes",
            modelName, numberOfRoutes);

        var totalStopwatch = Stopwatch.StartNew();

try
{
    TrainingSet trainingSet = new TrainingSet { Sequences = new List<Sequence>() };

    StatusTracker.Status = "Creating Routes";
    var edgeSequences = await CreateRoute(numberOfRoutes, minLength, maxLength);
    StatusTracker.Status = $"Created {edgeSequences.Count} routes";

    var resultsBag = new ConcurrentBag<Sequence>();
    var semaphore = new SemaphoreSlim(40);
    var tasks = new List<Task>();
    var sequenceCounter = 1;

    // Locks for thread-safety
    var hashSetLock = new object();
    var counterLock = new object();
    var uniqueSequences = new HashSet<Sequence>();

    _logger.LogInformation("[C# Service]: Processing {RouteCount} routes with max 40 concurrent tasks", edgeSequences.Count);

    foreach (var edges in edgeSequences)
    {
        await semaphore.WaitAsync();

        tasks.Add(Task.Run(async () =>
        {
            int currentSeq;
            lock (counterLock) currentSeq = sequenceCounter++;

            var routeStopwatch = Stopwatch.StartNew();
            StatusTracker.Status = $"Processing sequence {currentSeq} of {edgeSequences.Count}";

            try
            {
                var seq = new Sequence
                {
                    Edges = await GetEdgeVectors(edges),
                    TotalTime = await CreateTimeForRouteAsync(edges)
                };

                bool added;
                lock (hashSetLock)
                {
                    added = uniqueSequences.Add(seq);
                }

                if (added)
                {
                    resultsBag.Add(seq);
                    _logger.LogDebug("[C# Service]: Route {RouteId} finished on thread {ThreadId} in {ElapsedMs} ms",
                        currentSeq, Environment.CurrentManagedThreadId, routeStopwatch.ElapsedMilliseconds);
                }
                else
                {
                    _logger.LogDebug("[C# Service]: Duplicate route {RouteId} ignored", currentSeq);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[C# Service]: Error processing route {RouteId}", currentSeq);
            }
            finally
            {
                semaphore.Release();
            }
        }));
    }

    await Task.WhenAll(tasks);

    trainingSet.Sequences = uniqueSequences.ToList();

    _logger.LogInformation("[C# Service]: All routes processed. Total sequences: {SequenceCount}", uniqueSequences.Count);


            await UploadTrainingSetAsync(trainingSet, modelName);

            StatusTracker.Status = "Idle";
            _logger.LogInformation("[C# Service]: Training set creation completed successfully for model: {ModelName}", modelName);

            return "Training Done";
        }
        catch (Exception ex)
        {
            totalStopwatch.Stop();
            _logger.LogError(ex, "[C# Service]: Training set creation failed for model: {ModelName} after {ElapsedMs} ms",
                modelName, totalStopwatch.ElapsedMilliseconds);
            StatusTracker.Status = "Error: " + ex.Message;
            throw;
        }
    }
}