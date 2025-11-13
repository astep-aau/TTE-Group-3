using System.Text.Json;
using trainingService.Domain;
using Helpers;
using System.Security.Cryptography;

namespace TrainingService.Services
{
    //Denne service 
    public class TrainingService
    {
        // Shared HttpClient for the entire service
        private static readonly HttpClient client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10)
        };
    
        //Laver en instans af python runneren.
        private readonly runPythonScript _pythonRunner = new runPythonScript();
        //public StatusTracker StatusTracker = new StatusTracker;
        
        //Første del af servicen, den står for at lave en rute/sekvens af veje.
        public async Task<List<List<int>>> CreateRoute()
        {
            try
            {
                int numberOfSequences = 1000;
                int minLength = 5;
                int maxLength = 150;

                using var client = new HttpClient();
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
                Console.WriteLine($"Error calling time API: {ex.Message}");
                return 0.0;
            }
        }

        public List<double[]> GetEdgeVectors(List<int> edges)
        {
            string jsonArg = JsonSerializer.Serialize(edges);
            string output = _pythonRunner.RunPythonScript("Helpers/getEdgeToVectors.py", $"\"{jsonArg}\"");

            if (string.IsNullOrWhiteSpace(output))
                return new List<double[]>();

            try
            {
                var vectors = JsonSerializer.Deserialize<List<List<double>>>(output);
                return vectors?.Select(v => v.ToArray()).ToList() ?? new List<double[]>();
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"[ERROR] Could not parse vector output: {ex.Message}");
                return new List<double[]>();
            }
        }

        public void LstmTraining()
        {
            StatusTracker.Status = "LSTM Training";
            var output = _pythonRunner.RunPythonScript("Helpers/LSTMTraining.py");
        }
        
        //Det her er 3 del af servicen, det er den der kalder de 2 andre metoder og sørger for at det køre.
        public async Task<string> CreateTrainingSet()
        {
            //Laver et nyt object af vores Model "TrainingSet"
            TrainingSet trainingSet = new TrainingSet { Sequences = new List<Sequence>() };

            //Laver alle vores Ruter
            StatusTracker.Status = "Creating Routes";
            var edgeSequences = await CreateRoute();
            StatusTracker.Status = $"Created {edgeSequences.Count} routes";
            //For hver rute tjekker vi hvad den totale tid er.
            var sequenceCounter = 1;
            foreach (var edges in edgeSequences){
                StatusTracker.Status = $"Processing sequence {sequenceCounter} of {edgeSequences.Count}";
                sequenceCounter++;
                Console.WriteLine($"Processing route: [{string.Join(", ", edges)}]");

                double totalTime = await CreateTimeForRouteAsync(edges); // async call, but sequential
                List<double[]> replacedEdges = GetEdgeVectors(edges);

                var seq = new Sequence
                {
                    Edges = replacedEdges,
                    TotalTime = totalTime
                };
                
                trainingSet.Sequences.Add(seq);
            }
            
            var json = JsonSerializer.Serialize(trainingSet);
            File.WriteAllText("Helpers/Datasets/TrainingSet.JSON", json);

            LstmTraining();
            
            StatusTracker.Status = "Idle";
            return "Training Done";
        }
    }
}