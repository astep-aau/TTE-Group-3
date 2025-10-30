using System.Text.Json;
using trainingService.Domain;
using Helpers;

namespace TrainingService.Services
{
    //Denne service 
    public class TrainingService
    {
        //Laver en instans af python runneren.
        private readonly runPythonScript _pythonRunner = new runPythonScript();
        //public StatusTracker StatusTracker = new StatusTracker;
        
        //Første del af servicen, den står for at lave en rute/sekvens af veje.
        public List<List<int>> CreateRoute()
        {
            StatusTracker.Status = "Creating Routes";
            //Laver en liste af sekvenser, som køre fra python prossessen.
            string sequenceJson = _pythonRunner.RunPythonScript("Helpers/dataCreation.py");
            
            //Laver vores Json om til en List af List af sekvenser (C# Object)
            var edgeSequences = JsonSerializer.Deserialize<List<List<int>>>(sequenceJson);
            
            //Retunere vores sekvenser, hvis empty retunere et empty Object.
            return edgeSequences ?? new List<List<int>>();
        }

        // Anden del af servicen, den står for at tage alle vores edges og udregne en samlet tid for sekvensen
        public double CreateTimeForRoute(List<int> edges)
        {
            //Tager argumenterne til processen, det her er de sekvenser vi lavede før.
            string jsonArg = JsonSerializer.Serialize(edges);
            //Køre vores process, der udregner tiden.
            string output = _pythonRunner.RunPythonScript("Helpers/timeCreation.py", $"\"{jsonArg}\"");

            //Hvis den er tom, skriver vi 0
            if (string.IsNullOrWhiteSpace(output))
                return 0.0;
            
            //Her summere vi alle tiderne sammen.
            try {
                var times = JsonSerializer.Deserialize<List<double>>(output);
                return times?.Sum() ?? 0.0;
            }
            catch (JsonException ex) {
                Console.WriteLine($"Failed to parse Python output: {ex.Message}");
                return 0.0;
            }
        }
        
        public List<double[]> GetEdgeVectors(List<int> edges)
        {
            string jsonArg = JsonSerializer.Serialize(edges);
            string output = _pythonRunner.RunPythonScript("/Users/emilskov/RiderProjects/P5 - Time Travel Estimation/training-service/Helpers/getEdgeToVectors.py", $"\"{jsonArg}\"");

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
            var output = _pythonRunner.RunPythonScript("/Users/emilskov/RiderProjects/P5 - Time Travel Estimation/training-service/Helpers/LSTMTraining.py");
        }
        
        //Det her er 3 del af servicen, det er den der kalder de 2 andre metoder og sørger for at det køre.
        public string CreateTrainingSet()
        {
            //Laver et nyt object af vores Model "TrainingSet"
            TrainingSet trainingSet = new TrainingSet { Sequences = new List<Sequence>() };
            
            //Laver alle vores Ruter
            var edgeSequences = CreateRoute();
            //For hver rute tjekker vi hvad den totale tid er.
            var sequenceCounter = 1;
            object counterLock = new object(); // for updating sequenceCounter safely
            object listLock = new object(); // for adding to trainingSet.Sequences safely

            Parallel.ForEach(edgeSequences,
                new ParallelOptions { MaxDegreeOfParallelism = 2 },
                edges =>
                {
                    double totalTime = CreateTimeForRoute(edges);
                    List<double[]> replacedEdges = GetEdgeVectors(edges);

                    var seq = new Sequence
                    {
                        Edges = replacedEdges,
                        TotalTime = totalTime
                    };

                    // Safely add to shared list
                    lock (listLock)
                    {
                        trainingSet.Sequences.Add(seq);
                    }

                    // Update status safely
                    lock (counterLock)
                    {
                        sequenceCounter++;
                        StatusTracker.Status = $"Estimating Total Time For Routes ({sequenceCounter} / {edgeSequences.Count})";
                    }
                });
            
            var json = JsonSerializer.Serialize(trainingSet);
            File.WriteAllText("Helpers/Datasets/TrainingSet.JSON", json);

            LstmTraining();
            
            StatusTracker.Status = "Idle";
            return "Training Done";
        }
    }
}