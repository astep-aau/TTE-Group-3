using System.Text.Json;
using trainingService.Domain;
using Helpers;
using System.Security.Cryptography;

namespace TrainingService.Services
{
    //Denne service 
    public class TrainingService
    {
        //Laver en instans af python runneren.
        private readonly runPythonScript _pythonRunner = new runPythonScript();
        //public StatusTracker StatusTracker = new StatusTracker;
        
        //Første del af servicen, den står for at lave en rute/sekvens af veje.
        public async Task<List<List<int>>> CreateRoute()
        {
            try
            {
                int numberOfSequences = 5;
                int minLength = 5;
                int maxLength = 5;

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
        public async Task<double> CreateTimeForRouteAsync(List<int> edges){
            if (edges == null || edges.Count == 0)
                return 0.0;
            Console.WriteLine($"Calculating time for edges: [{string.Join(", ", edges)}]");
            using var client = new HttpClient();
            string url = "http://127.0.0.1:8000/Python/calculate-route-time";

            try{
                string jsonBody = JsonSerializer.Serialize(edges);
                var content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");

                HttpResponseMessage response = await client.PostAsync(url, content);
                response.EnsureSuccessStatusCode();

                string responseJson = await response.Content.ReadAsStringAsync();
                var times = JsonSerializer.Deserialize<List<double>>(responseJson);

                return times?.Sum() ?? 0.0;
            }catch (Exception ex){
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
            var edgeSequences = await CreateRoute();
            //For hver rute tjekker vi hvad den totale tid er.
            var sequenceCounter = 1;
            object counterLock = new object(); // for updating sequenceCounter safely
            object listLock = new object(); // for adding to trainingSet.Sequences safely

            // Create a list of tasks for parallel execution
            var tasks = edgeSequences.Select(async edges => {
                // Call your async API function
                double totalTime = await CreateTimeForRouteAsync(edges);
                List<double[]> replacedEdges = GetEdgeVectors(edges);

                var seq = new Sequence{
                    Edges = replacedEdges,
                    TotalTime = totalTime
                };

                // Safely add to shared list
                lock (listLock){
                    trainingSet.Sequences.Add(seq);
                }

                // Safely update status
                lock (counterLock){
                    sequenceCounter++;
                    StatusTracker.Status = $"Estimating Total Time For Routes ({sequenceCounter} / {edgeSequences.Count})";
                }
            });

            // Await all tasks to complete
            await Task.WhenAll(tasks);
            
            var json = JsonSerializer.Serialize(trainingSet);
            File.WriteAllText("Helpers/Datasets/TrainingSet.JSON", json);

            LstmTraining();
            
            StatusTracker.Status = "Idle";
            return "Training Done";
        }
    }
}