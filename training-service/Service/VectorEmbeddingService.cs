using Helpers;

namespace VectorEmbeddingService.Services
{
    public class VectorEmbeddingService
    {
        private readonly runPythonScript _pythonRunner = new runPythonScript();
        public string VectorEmbedding()
        {
            var vectorEmbedding = _pythonRunner.RunPythonScript("Helpers/Edge2Vec.py");
            return vectorEmbedding;
        }
    }
}