using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LogAnalysisApi;
using Microsoft.Extensions.Logging;

namespace LogAnalysisApi
{
    [ApiController]
    [Route("api/[controller]")]
    public class LogAnalysisController : ControllerBase
    {
        private readonly LogAnalysisService _logAnalysisService;
        private readonly IEmailService _emailService;
        private readonly ErrorRatioCheckService _errorRatioCheckService;
        private readonly string uploadsRootPath;
        private readonly ILogger<LogAnalysisController> _logger;

        public LogAnalysisController(LogAnalysisService logAnalysisService, IEmailService emailService, ErrorRatioCheckService errorRatioCheckService, ILogger<LogAnalysisController> logger)
        {
            _logAnalysisService = logAnalysisService ?? throw new ArgumentNullException(nameof(logAnalysisService));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
            _errorRatioCheckService = errorRatioCheckService ?? throw new ArgumentNullException(nameof(errorRatioCheckService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            uploadsRootPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
            if (!Directory.Exists(uploadsRootPath))
                Directory.CreateDirectory(uploadsRootPath);
        }

        [HttpGet("files")]
        public ActionResult<List<string>> GetFiles()
        {
            try
            {
                var files = Directory.GetFiles(uploadsRootPath)
                    .Select(Path.GetFileName)
                    .ToList();
                return Ok(files);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading files from uploads directory.");
                return StatusCode(500, new { message = "Error reading files.", error = ex.Message });
            }
        }

        [HttpPost("trigger-popup-notification")]
        public IActionResult TriggerPopupNotification([FromBody] PopupNotificationRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new { message = "Notification message is required." });
            }

            _logger.LogInformation("Popup notification triggered with message: {Message}", request.Message);

            return Ok(new { message = "Popup notification triggered." });
        }

        [HttpPost("trigger-alert")]
        public async Task<IActionResult> TriggerAlert()
        {
            try
            {
                await _errorRatioCheckService.TriggerAlertCheckAsync();
                return Ok("Alert check triggered successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to trigger alert check.");
                return StatusCode(500, new { message = "Failed to trigger alert check.", error = ex.Message });
            }
        }

        [HttpGet("count-log-types")]
        public ActionResult<Dictionary<string, int>> GetLogCounts([FromQuery] string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return BadRequest(new { message = "filePath is required and cannot be empty." });

            try
            {
                var fullPath = GetFullFilePath(filePath);
                var result = _logAnalysisService.CountLogTypes(fullPath);
                return Ok(result);
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogWarning(ex, "File not found: {FilePath}", filePath);
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting log types for file: {FilePath}", filePath);
                return StatusCode(500, new { message = "Server error.", error = ex.Message });
            }
        }

        [HttpGet("count-log-types-per-hex")]
        public ActionResult<Dictionary<string, Dictionary<string, int>>> GetLogCountsPerHex([FromQuery] string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return BadRequest(new { message = "filePath is required and cannot be empty." });

            try
            {
                var fullPath = GetFullFilePath(filePath);
                var result = _logAnalysisService.CountLogTypesPerHex(fullPath);
                return Ok(result);
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogWarning(ex, "File not found: {FilePath}", filePath);
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting log types per hex for file: {FilePath}", filePath);
                return StatusCode(500, new { message = "Server error.", error = ex.Message });
            }
        }

        [HttpGet("calculate-time-differences")]
        public ActionResult<Dictionary<string, double>> GetTimeDifferences([FromQuery] string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return BadRequest(new { message = "filePath is required and cannot be empty." });

            try
            {
                var fullPath = GetFullFilePath(filePath);
                var result = _logAnalysisService.CalculateTimeDifferences(fullPath);
                return Ok(result);
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogWarning(ex, "File not found: {FilePath}", filePath);
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating time differences for file: {FilePath}", filePath);
                return StatusCode(500, new { message = "Server error.", error = ex.Message });
            }
        }

        [HttpGet("extract-topics")]
        public ActionResult<Dictionary<string, HashSet<string>>> GetTopicsAfterHex([FromQuery] string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return BadRequest(new { message = "filePath is required and cannot be empty." });

            try
            {
                var fullPath = GetFullFilePath(filePath);
                var result = _logAnalysisService.ExtractTopicsAfterHex(fullPath);
                return Ok(result);
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogWarning(ex, "File not found: {FilePath}", filePath);
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting topics for file: {FilePath}", filePath);
                return StatusCode(500, new { message = "Server error.", error = ex.Message });
            }
        }

        [HttpGet("topic-wise-time-diff")]
        public ActionResult<Dictionary<string, Dictionary<string, double>>> GetTopicWiseTimeDiff([FromQuery] string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return BadRequest(new { message = "filePath is required and cannot be empty." });

            try
            {
                var fullPath = GetFullFilePath(filePath);
                var result = _logAnalysisService.CalculateTimeDiffByTopic(fullPath);
                return Ok(result);
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogWarning(ex, "File not found: {FilePath}", filePath);
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating topic-wise time differences for file: {FilePath}", filePath);
                return StatusCode(500, new { message = "Server error.", error = ex.Message });
            }
        }

        [HttpGet("error-to-hex-ratio")]
        public ActionResult<object> GetErrorToHexRatio([FromQuery] string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return BadRequest(new { message = "filePath is required and cannot be empty." });

            try
            {
                var fullPath = GetFullFilePath(filePath);
                var (ratios, average) = _logAnalysisService.CalculateErrorToHexRatio(fullPath);
                return Ok(new { ratios, average });
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogWarning(ex, "File not found: {FilePath}", filePath);
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating error to hex ratio for file: {FilePath}", filePath);
                return StatusCode(500, new { message = "Server error.", error = ex.Message });
            }
        }

        private string GetFullFilePath(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentException("File name cannot be null or empty.", nameof(fileName));

            return Path.Combine(uploadsRootPath, fileName);
        }

        [HttpPost("send-email")]
        [Consumes("application/json")]
        public async Task<IActionResult> SendEmail([FromBody] EmailRequest emailRequest)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("ModelState invalid: {@Errors}", errors);
                return BadRequest(new { message = "Invalid model state.", errors });
            }

            if (emailRequest == null || string.IsNullOrWhiteSpace(emailRequest.Subject) || string.IsNullOrWhiteSpace(emailRequest.Body))
            {
                _logger.LogWarning("EmailRequest is null or missing required fields: {@EmailRequest}", emailRequest);
                return BadRequest("Invalid email request. Subject and Body are required.");
            }

            try
            {
                _logger.LogInformation("Received SendEmail request: {@EmailRequest}", emailRequest);
                await _emailService.SendAlertEmailAsync(emailRequest.Subject, emailRequest.Body);
                return Ok("Email sent successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email with subject {Subject}", emailRequest.Subject);
                return StatusCode(500, new { message = "Failed to send email.", error = ex.Message });
            }
        }

        [HttpGet("test")]
        public IActionResult Test() => Ok("API is working.");

        public class EmailRequest
        {
            public string? Subject { get; set; }
            public string? Body { get; set; }
        }
    }
}
