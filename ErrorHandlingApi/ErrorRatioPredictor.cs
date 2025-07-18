using Microsoft.ML;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace LogAnalysisApi
{
    public class ErrorRatioPredictor
    {
        private readonly string _modelPath = Path.Combine(AppContext.BaseDirectory, "model.zip");
        private readonly MLContext _mlContext = new();
        private readonly ILogger<ErrorRatioPredictor> _logger;

        public ErrorRatioPredictor(ILogger<ErrorRatioPredictor> logger)
        {
            _logger = logger;
        }

        public void TrainModel(IEnumerable<ErrorRatioData> trainingData)
        {
            if (trainingData == null || !trainingData.Any())
            {
                _logger.LogError("Training set has 0 instances, aborting training.");
                throw new InvalidOperationException("Training set has 0 instances, aborting training.");
            }

            var data = _mlContext.Data.LoadFromEnumerable(trainingData);

            var pipeline = _mlContext.Transforms
                .Concatenate("Features", nameof(ErrorRatioData.SessionCount), nameof(ErrorRatioData.ErrorCount))
                .Append(_mlContext.Regression.Trainers.Sdca(labelColumnName: "ErrorRatio"));
                
            var model = pipeline.Fit(data);
            _mlContext.Model.Save(model, data.Schema, _modelPath);
            _logger.LogInformation("Model trained and saved to {ModelPath}", _modelPath);
        }

        public float Predict(int sessionCount, int errorCount)
        {
            if (!File.Exists(_modelPath))
            {
                _logger.LogWarning("Model file not found at {ModelPath}. Returning default score of 0.", _modelPath);
                return 0;
            }

            var model = _mlContext.Model.Load(_modelPath, out _);
            var predictionEngine = _mlContext.Model.CreatePredictionEngine<ErrorRatioData, ErrorRatioPrediction>(model);

            var input = new ErrorRatioData
            {
                SessionCount = sessionCount,
                ErrorCount = errorCount
            };

            return predictionEngine.Predict(input).Score;
        }
    }
}
