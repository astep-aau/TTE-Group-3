using System.Text.Json;
using TrainingService.Domain;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace TrainingService.Services
{
    //Denne service 
    public class Service
    {
        private static readonly HttpClient client = new HttpClient{
            Timeout = TimeSpan.FromMinutes(1000)
        };
        private readonly ILogger<Service> _logger;
        public Service(ILogger<Service> logger){
            _logger = logger;
        }

        
        //Første del af servicen, den står for at lave en rute/sekvens af veje.
        public async Task<List<List<int>>> CreateRoute(int numberOfSequences, int minLength, int maxLength)
        {
            try
            {
                _logger.LogInformation("Creating routes");
                string url = $"http://127.0.0.1:8000/Python/generate-routes/{numberOfSequences}/{minLength}/{maxLength}";

                // Send GET request
                var httpResponse = await client.GetAsync(url);
                httpResponse.EnsureSuccessStatusCode(); // throws if not 2xx

                string responseJson = await httpResponse.Content.ReadAsStringAsync();

                List<List<int>> edgeSequences = JsonSerializer.Deserialize<List<List<int>>>(JsonDocument.Parse(responseJson).RootElement.GetProperty("routes").GetRawText())!;

                _logger.LogInformation($"Created {edgeSequences.Count} routes.");
                // Return the routes or empty list if null
                return edgeSequences ?? new List<List<int>>();
            }
            catch (Exception ex)
            {
                StatusTracker.Status = "Error creating routes: " + ex.Message;
                throw; // same behavior as before
            }
        }

        // Anden del af servicen, den står for at tage alle vores edges og udregne en samlet tid for sekvensen
        public async Task<double> CreateTimeForRouteAsync(List<int> edges)
        {
            if (edges == null || edges.Count == 0)
                return 0.0;

            try
            {
                string url = "http://127.0.0.1:8000/Python/calculate-route-time";
                string jsonBody = JsonSerializer.Serialize(edges);
                using var content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");

                HttpResponseMessage response = await client.PostAsync(url, content);
                response.EnsureSuccessStatusCode();

                string responseJson = await response.Content.ReadAsStringAsync();

                // Deserialize as a list of doubles (or change if API returns an object)
                var times = JsonSerializer.Deserialize<List<double>>(responseJson);

                return times?.Sum() ?? 0.0;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error calling time API: {ex.Message}");
                return 0.0;
            }
        }

        public async Task<List<double[]>> GetEdgeVectors(List<int> edges)
        {
            if (edges == null || edges.Count == 0)
                return new List<double[]>();

            try
            {
                string url = "http://127.0.0.1:8000/Python/vectors";
                string jsonBody = JsonSerializer.Serialize(edges);
                using var content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");

                HttpResponseMessage response = await client.PostAsync(url, content);
                response.EnsureSuccessStatusCode();

                string responseJson = await response.Content.ReadAsStringAsync();

                // Deserialize as a list of doubles (or change if API returns an object)
                var vector = JsonSerializer.Deserialize<List<double[]>>(responseJson);

                return vector ?? new List<double[]>();
            }
            catch (JsonException ex)
            {
                _logger.LogError($"[ERROR] Could not parse vector output: {ex.Message}");
                return new List<double[]>();
            }
        }

        public async Task LstmTraining(string ModelName)
        {
            StatusTracker.Status = "LSTM Training";
            using var client = new HttpClient();
            string url = $"http://127.0.0.1:8000/Python/train-lstm/{ModelName}";

            // POST request with no body
            HttpResponseMessage response = await client.PostAsync(url, null);
            response.EnsureSuccessStatusCode();

            string responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation(responseContent);
        }

        public async Task UploadTrainingSetAsync(TrainingSet trainingSet){
            byte[] fileBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(trainingSet));
            using var streamContent = new ByteArrayContent(fileBytes);
            using var form = new MultipartFormDataContent();
            form.Add(streamContent, "file", "TrainingSet.json"); // meaningful filename

            HttpResponseMessage response = await client.PostAsync("http://127.0.0.1:8000/Python/TrainingFile", form);
            response.EnsureSuccessStatusCode();
    }
        
        //Det her er 3 del af servicen, det er den der kalder de 2 andre metoder og sørger for at det køre.
        public async Task<string> CreateTrainingSet(
            string modelName,
            int numberOfRoutes,
            int minLength,
            int maxLength)
        {
            //Laver et nyt object af vores Model "TrainingSet"
            TrainingSet trainingSet = new TrainingSet { Sequences = new List<Sequence>() };

            //Laver alle vores Ruter
            StatusTracker.Status = "Creating Routes";
            var edgeSequences = await CreateRoute(numberOfRoutes, minLength, maxLength);
            StatusTracker.Status = $"Created {edgeSequences.Count} routes";
            //For hver rute tjekker vi hvad den totale tid er.
            
            // Create a thread-safe collection for results
            var resultsBag = new ConcurrentBag<Sequence>();

            // Semaphore to limit parallelism to 4 routes at a time
            var semaphore = new SemaphoreSlim(4);
            var totalStopwatch = Stopwatch.StartNew();
            var tasks = new List<Task>();
            int sequenceCounter = 1;            
            foreach (var edges in edgeSequences)
            {
                await semaphore.WaitAsync();

                tasks.Add(Task.Run(async () =>
                {
                    int currentSeq;
                    lock (resultsBag) currentSeq = sequenceCounter++;
                    var routeStopwatch = Stopwatch.StartNew();
                    StatusTracker.Status = $"Processing sequence {currentSeq} of {edgeSequences.Count}";

                    var seq = new Sequence
                    {
                        Edges = await GetEdgeVectors(edges),
                        TotalTime = await CreateTimeForRouteAsync(edges)
                    };

                    resultsBag.Add(seq);
                    _logger.LogInformation("[Route {RouteId}] Finished on thread {ThreadId} in {ElapsedMs} ms", 
                        currentSeq, Thread.CurrentThread.ManagedThreadId, routeStopwatch.ElapsedMilliseconds);
                    semaphore.Release();
                }));
            }

            // Wait for all routes to finish
            await Task.WhenAll(tasks);
            totalStopwatch.Stop();
            _logger.LogInformation("All routes finished in {ElapsedMs} ms", 
                totalStopwatch.ElapsedMilliseconds);

            // Add all results to your training set
            trainingSet.Sequences.AddRange(resultsBag);
            
            await UploadTrainingSetAsync(trainingSet);

            await LstmTraining(modelName);
            
            StatusTracker.Status = "Idle";
            return "Training Done";
        }
    }
}