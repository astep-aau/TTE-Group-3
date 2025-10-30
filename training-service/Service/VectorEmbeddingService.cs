using Helpers;
using trainingService.Domain;

namespace VectorEmbeddingService.Services
{
    public class VectorEmbeddingService
    {
        private readonly runPythonScript _pythonRunner = new runPythonScript();
        public void VectorEmbedding()
        {
            StatusTracker.Status = "Embedding Vectors";
            string output = _pythonRunner.RunPythonScript("Helpers/Edge2Vec.py");
            foreach (var line in output.Split('\n'))
            {
                if (line.StartsWith("#PROGRESS"))
                {
                    StatusTracker.Status = line.Substring(10); // "1 out of 11000"
                }
            }
        }
    }
}