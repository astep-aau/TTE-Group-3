using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using trainingService.Domain;

namespace TrainingService.Services
{
    public class TrainingService
    {
        // Main method to start training
        public TrainingSet StartTraining(int numSequences = 10, int sequenceLength = 5)
        {
            var trainingSet = new TrainingSet
            {
                Sequences = new List<Sequence>()
            };

            // 1️⃣ Call Python helper to generate sequences
            string sequenceJson = RunPythonScript("Helpers/dataCreation.py", numSequences.ToString());
            var edgeSequences = JsonSerializer.Deserialize<List<List<int>>>(sequenceJson);

            if (edgeSequences == null)
                return trainingSet;

            // 2️⃣ For each sequence, call Python helper to calculate total time
            foreach (var edges in edgeSequences)
            {
                double totalTime = GetTotalTimeFromPython(edges.ToArray());

                // 3️⃣ Create Sequence object
                var seq = new Sequence
                {
                    Edges = edges,
                    TotalTime = (int)Math.Round(totalTime)
                };

                // 4️⃣ Add to training set
                trainingSet.Sequences.Add(seq);
            }

            return trainingSet;
        }

        // Run any Python script and return its standard output
        private string RunPythonScript(string scriptPath, string args = "")
        {
            var psi = new ProcessStartInfo
            {
                FileName = "python3",
                Arguments = string.IsNullOrWhiteSpace(args) ? scriptPath : $"{scriptPath} {args}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(psi))
            {
                string output = process.StandardOutput.ReadToEnd();
                string errors = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (!string.IsNullOrEmpty(errors))
                    Console.WriteLine("Python errors: " + errors);

                return output.Trim();
            }
        }

        // Call Python total-time helper for a sequence of edges
        // Call Python total-time helper for a sequence of edges
        // Call Python total-time helper for a sequence of edges
        private double GetTotalTimeFromPython(int[] edgeSequence)
        {
            string jsonArg = JsonSerializer.Serialize(edgeSequence);
            string output = RunPythonScript("Helpers/timeCreation.py", $"\"{jsonArg}\"");

            if (string.IsNullOrWhiteSpace(output))
                return 0.0;

            try
            {
                // Parse JSON array returned by Python
                var times = JsonSerializer.Deserialize<List<double>>(output);

                if (times == null || times.Count == 0)
                    return 0.0;

                // Sum all edge times as float
                double totalTimeFloat = 0.0;
                foreach (var t in times)
                    totalTimeFloat += t;

                return totalTimeFloat;
            }
            catch (JsonException ex)
            {
                Console.WriteLine("Failed to parse Python output: " + ex.Message);
                return 0.0;
            }
        }
    }
}