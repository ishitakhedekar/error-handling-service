using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LogAnalysisApi
{
    public class ErrorRatioCheckService : BackgroundService
    {
        private readonly IEmailService _emailService;
        private readonly ILogger<ErrorRatioCheckService> _logger;
        private readonly LogAnalysisService _logAnalysisService;
        private readonly string _logDirectoryPath;
        private readonly ErrorRatioPredictor _predictor;

        public ErrorRatioCheckService(
            IEmailService emailService,
            ILogger<ErrorRatioCheckService> logger,
            LogAnalysisService logAnalysisService,
            ErrorRatioPredictor predictor)
        {
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _logAnalysisService = logAnalysisService ?? throw new ArgumentNullException(nameof(logAnalysisService));
            _predictor = predictor ?? throw new ArgumentNullException(nameof(predictor));
            _logDirectoryPath = Path.Combine(AppContext.BaseDirectory, "uploads");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await CheckAndSendAlertsAsync();
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        private async Task CheckAndSendAlertsAsync()
        {
            try
            {
                if (!Directory.Exists(_logDirectoryPath))
                {
                    Directory.CreateDirectory(_logDirectoryPath);
                    _logger.LogInformation("Created directory {Directory}", _logDirectoryPath);
                }

                var filesData = await _logAnalysisService.GetAllFilesDataAsync(_logDirectoryPath);

                if (filesData == null || !filesData.Any())
                {
                    _logger.LogInformation("No log files found or no data to process.");
                    return;
                }

                var trainingData = filesData
                    .Where(f => f.SessionCount > 0)
                    .Select(f => new ErrorRatioData
                    {
                        SessionCount = f.SessionCount,
                        ErrorCount = f.ErrorCount,
                        ErrorRatio = (float)(f.ErrorRatio ?? 0)
                    }).ToList();

                if (trainingData.Any())
                {
                    _predictor.TrainModel(trainingData);
                }

                foreach (var file in filesData)
                {
                    if (file == null || file.SessionCount == 0) continue;

                    float predicted = _predictor.Predict(file.SessionCount, file.ErrorCount);
                    float actual = (float)(file.ErrorRatio ?? 0);

                    if (actual > predicted * 1.2)
                    {
                        string subject = $"Alert: High Error Ratio Detected in {file.FileName}";
                        string body = $@"
<html>
<head>
<style>
  body {{ font-family: Arial, sans-serif; }}
  .alert-box {{ border: 1px solid red; padding: 10px; background-color: #ffe6e6; }}
  .highlight {{ color: red; font-weight: bold; }}
</style>
</head>
<body>
  <div class='alert-box'>
    <h2> High Error Ratio Detected!</h2>
    <p><strong>File:</strong> {file.FileName}</p>
    <p><strong>Sessions:</strong> {file.SessionCount}</p>
    <p><strong>Errors:</strong> {file.ErrorCount}</p>
    <p><strong>Actual Ratio:</strong> <span class='highlight'>{actual:P2}</span></p>
    <p><strong>Predicted Threshold:</strong> <span class='highlight'>{predicted:P2}</span></p>
    <p>This error ratio exceeds the expected limit. Please investigate immediately.</p>
  </div>
</body>
</html>
";

                        if (_emailService != null)
                        {
                            await _emailService.SendAlertEmailAsync(subject, body);
                            _logger.LogInformation("Alert email sent for file {File} with error ratio {Actual:P2}.", file.FileName, actual);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check error ratios or send alert emails.");
            }
        }

        public async Task TriggerAlertCheckAsync()
        {
            await CheckAndSendAlertsAsync();
        }
    }
}
