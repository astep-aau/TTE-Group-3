namespace TrainingService.Configuration;
    
    /// <summary>
    /// Configuration settings for the Python backend service.
    /// </summary>
    public class PythonBackendSettings
    {
        /// <summary>
        /// Gets or sets the base URL of the Python backend service.
        /// </summary>
        public string BaseUrl { get; set; } = string.Empty;
    
        /// <summary>
        /// Gets or sets the endpoint paths for various Python backend operations.
        /// </summary>
        public PythonEndpoints Endpoints { get; set; } = new();
    }
    
    /// <summary>
    /// Endpoint paths for Python backend API operations.
    /// </summary>
    public class PythonEndpoints
    {
        /// <summary>
        /// Endpoint template for generating routes.
        /// </summary>
        public string GenerateRoutes { get; set; } = string.Empty;
    
        /// <summary>
        /// Endpoint for calculating route time.
        /// </summary>
        public string CalculateRouteTime { get; set; } = string.Empty;
    
        /// <summary>
        /// Endpoint for retrieving edge vectors.
        /// </summary>
        public string Vectors { get; set; } = string.Empty;
    
        /// <summary>
        /// Endpoint for uploading training files.
        /// </summary>
        public string TrainingFile { get; set; } = string.Empty;
    
        /// <summary>
        /// Endpoint template for initiating LSTM training.
        /// </summary>
        public string TrainLstm { get; set; } = string.Empty;
    }