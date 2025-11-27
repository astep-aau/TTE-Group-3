using System.Text.Json;
using trainingService.Domain;
using System.Security.Cryptography;

namespace TrainingService.Services
{
    //Denne service 
    public class TrainingService
    {
        private static readonly HttpClient client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(1000)
        };

        
        //Første del af servicen, den står for at lave en rute/sekvens af veje.
        public async Task<List<List<int>>> CreateRoute(int numberOfSequences, int minLength, int maxLength)
        {
            try
            {
                string url = $"http://127.0.0.1:8000/Python/generate-routes/{numberOfSequences}/{minLength}/{maxLength}";

                // Send GET request
                var httpResponse = await client.GetAsync(url);
                httpResponse.EnsureSuccessStatusCode(); // throws if not 2xx

                string responseJson = await httpResponse.Content.ReadAsStringAsync();

                List<List<int>> edgeSequences = JsonSerializer.Deserialize<List<List<int>>>(JsonDocument.Parse(responseJson).RootElement.GetProperty("routes").GetRawText())!;

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
        public async Task<double> CreateTimeForRouteAsync(List<int> edges, int? timeBucket = null)
        {
            if (edges == null || edges.Count == 0)
                return 0.0;

            try
            {
                string url = "http://127.0.0.1:8000/Python/calculate-route-time";
                if (timeBucket.HasValue)
                {
                    url += $"?timeBucket={timeBucket.Value}";
                }

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
                Console.WriteLine($"Error calling time API: {ex.Message}");
                return 0.0;
            }
        }

        public async Task<List<double[]>> GetEdgeVectors(List<int> edges, int? timeBucket = null)
        {
            if (edges == null || edges.Count == 0)
                return new List<double[]>();

            try
            {
                string url = "http://127.0.0.1:8000/Python/vectors";
                if (timeBucket.HasValue)
                {
                    url += $"?timeBucket={timeBucket.Value}";
                }

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
                Console.WriteLine($"[ERROR] Could not parse vector output: {ex.Message}");
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
            Console.WriteLine(responseContent);
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
            var sequenceCounter = 1;
            var random = new Random();

            foreach (var edges in edgeSequences){
                StatusTracker.Status = $"Processing sequence {sequenceCounter} of {edgeSequences.Count}";
                sequenceCounter++;
                Console.WriteLine($"Processing route: [{string.Join(", ", edges)}]");

                // Generate a random time bucket (0-287)
                int timeBucket = random.Next(0, 288);

                double totalTime = await CreateTimeForRouteAsync(edges, timeBucket); // async call, but sequential
                List<double[]> replacedEdges = await GetEdgeVectors(edges, timeBucket);

                var seq = new Sequence
                {
                    Edges = replacedEdges,
                    TotalTime = totalTime
                };
                
                trainingSet.Sequences.Add(seq);
            }

            var json = JsonSerializer.Serialize(trainingSet);
            File.WriteAllText("../Python/Service/Data/TrainingSet.JSON", json);

            await LstmTraining(modelName);

            StatusTracker.Status = "Idle";
            return "Training Done";
        }
    }
}