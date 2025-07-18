using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LogAnalysisApi
{
    public class LogAnalysisService
    {
        private readonly string defaultLogFile = "application.log";

        private List<string> ReadLogFile(string? logFilePath = null)
        {
            string path = logFilePath ?? defaultLogFile;

            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Log file path cannot be null or empty.", nameof(logFilePath));

            if (!File.Exists(path))
                throw new FileNotFoundException($"Log file not found: {Path.GetFullPath(path)}");

            return File.ReadAllLines(path)
                       .Select(line => line.Trim())
                       .Where(line => !string.IsNullOrWhiteSpace(line))
                       .ToList();
        }

        private bool IsValidHexId(string? hexId)
        {
            return !string.IsNullOrEmpty(hexId) && Regex.IsMatch(hexId, @"^[0-9A-Fa-f]+$") && !Regex.IsMatch(hexId, @"^0+$");
        }

        public Dictionary<string, int> CountLogTypes(string? logFilePath = null)
        {
            var lines = ReadLogFile(logFilePath);
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["DEBUG"] = 0,
                ["INFO"] = 0,
                ["WARN"] = 0,
                ["ERROR"] = 0,
                ["TRACE"] = 0
            };

            foreach (var line in lines)
            {
                if (line.Contains("DEBUG", StringComparison.OrdinalIgnoreCase)) counts["DEBUG"]++;
                else if (line.Contains("INFO", StringComparison.OrdinalIgnoreCase)) counts["INFO"]++;
                else if (line.Contains("WARN", StringComparison.OrdinalIgnoreCase)) counts["WARN"]++;
                else if (line.Contains("ERROR", StringComparison.OrdinalIgnoreCase)) counts["ERROR"]++;
                else if (line.Contains("TRACE", StringComparison.OrdinalIgnoreCase) || line.Contains("VERBOSE", StringComparison.OrdinalIgnoreCase)) counts["TRACE"]++;
            }

            return counts;
        }

        public Dictionary<string, Dictionary<string, int>> CountLogTypesPerHex(string? logFilePath = null)
        {
            var lines = ReadLogFile(logFilePath);
            var hexLogCounts = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
            var logLevels = new[] { "DEBUG", "INFO", "WARN", "ERROR", "TRACE" };

            foreach (var line in lines)
            {
                var parts = line.Split(new[] { ' ' }, 5, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 5) continue;

                var hexId = parts[0];
                if (!IsValidHexId(hexId)) continue;

                var logLevel = parts[4].Split(':')[0].ToUpper();

                if (!logLevels.Contains(logLevel)) continue;

                if (!hexLogCounts.ContainsKey(hexId))
                    hexLogCounts[hexId] = logLevels.ToDictionary(l => l, l => 0);

                hexLogCounts[hexId][logLevel]++;
            }

            return hexLogCounts;
        }

        public Dictionary<string, double> CalculateTimeDifferences(string? logFilePath = null)
        {
            var lines = ReadLogFile(logFilePath);
            var hexTimes = new Dictionary<string, List<DateTime>>();

            foreach (var line in lines)
            {
                var parts = line.Split(new[] { ' ' }, 5, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 5) continue;

                var hexId = parts[1];
                var timeString = parts[3];
                if (!IsValidHexId(hexId)) continue;
                if (!DateTime.TryParseExact(timeString, "yyyy-MM-ddTHH:mm:ss,fff", CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp)) continue;

                if (!hexTimes.ContainsKey(hexId)) hexTimes[hexId] = new List<DateTime>();
                hexTimes[hexId].Add(timestamp);
            }

            var diffs = new Dictionary<string, double>();
            foreach (var kvp in hexTimes)
            {
                var sortedTimes = kvp.Value.OrderBy(t => t).ToList();
                if (sortedTimes.Count < 2) continue;

                double totalSeconds = 0;
                for (int i = 1; i < sortedTimes.Count; i++)
                    totalSeconds += (sortedTimes[i] - sortedTimes[i - 1]).TotalSeconds;

                diffs[kvp.Key] = totalSeconds / (sortedTimes.Count - 1);
            }
            return diffs;
        }

        public Dictionary<string, HashSet<string>> ExtractTopicsAfterHex(string? logFilePath = null)
        {
            var lines = ReadLogFile(logFilePath);
            var result = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var line in lines)
            {
                var parts = line.Split(new[] { ' ' }, 5, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 5) continue;

                var hexId = parts[0];
                if (!IsValidHexId(hexId)) continue;

                var logBody = parts[4];
                var colonIndex = logBody.IndexOf(':');
                if (colonIndex < 0 || colonIndex == logBody.Length - 1) continue;

                var message = logBody.Substring(colonIndex + 1).Trim();
                var topic = message.Split(' ', '(')[0];

                if (!result.ContainsKey(hexId)) result[hexId] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (!string.IsNullOrEmpty(topic)) result[hexId].Add(topic);
            }
            return result;
        }

        public Dictionary<string, Dictionary<string, double>> CalculateTimeDiffByTopic(string? logFilePath = null)
        {
            var lines = ReadLogFile(logFilePath);
            var topicTimes = new Dictionary<string, Dictionary<string, List<DateTime>>>(StringComparer.OrdinalIgnoreCase);

            foreach (var line in lines)
            {
                var parts = line.Split(new[] { ' ' }, 5, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 5) continue;

                var hexId = parts[0];
                if (!IsValidHexId(hexId)) continue;

                var timeString = parts[3];
                var logBody = parts[4];
                var colonIndex = logBody.IndexOf(':');
                if (colonIndex < 0 || colonIndex == logBody.Length - 1) continue;

                var message = logBody.Substring(colonIndex + 1).Trim()
                    .Replace("started", "", StringComparison.OrdinalIgnoreCase)
                    .Replace("ended", "", StringComparison.OrdinalIgnoreCase)
                    .Trim();

                if (string.IsNullOrEmpty(message)) continue;

                var topic = message.Split(new[] { ' ', '(' }, StringSplitOptions.RemoveEmptyEntries)[0];
                if (!DateTime.TryParseExact(timeString, "yyyy-MM-ddTHH:mm:ss,fff", CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp)) continue;

                if (!topicTimes.ContainsKey(topic))
                    topicTimes[topic] = new Dictionary<string, List<DateTime>>(StringComparer.OrdinalIgnoreCase);

                if (!topicTimes[topic].ContainsKey(hexId))
                    topicTimes[topic][hexId] = new List<DateTime>();

                topicTimes[topic][hexId].Add(timestamp);
            }

            var result = new Dictionary<string, Dictionary<string, double>>(StringComparer.OrdinalIgnoreCase);
            foreach (var topicEntry in topicTimes)
            {
                var topic = topicEntry.Key;
                var hexGroups = topicEntry.Value;

                var topicDiffs = new Dictionary<string, double>();
                double totalDiff = 0;
                int count = 0;

                foreach (var hexEntry in hexGroups)
                {
                    var times = hexEntry.Value.OrderBy(t => t).ToList();
                    if (times.Count < 2) continue;

                    double diffSeconds = 0;
                    for (int i = 1; i < times.Count; i++)
                        diffSeconds += (times[i] - times[i - 1]).TotalSeconds;

                    double average = diffSeconds / (times.Count - 1);
                    topicDiffs[hexEntry.Key] = Math.Round(average, 3);
                    totalDiff += average;
                    count++;
                }

                topicDiffs["Average"] = count > 0 ? Math.Round(totalDiff / count, 3) : 0;
                result[topic] = topicDiffs;
            }
            return result;
        }

        public (Dictionary<string, double> ratios, double average) CalculateErrorToHexRatio(string? logFilePath = null)
        {
            var lines = ReadLogFile(logFilePath);
            var hexCounts = new Dictionary<string, (int errorCount, int totalCount)>(StringComparer.OrdinalIgnoreCase);

            foreach (var line in lines)
            {
                var parts = line.Split(new[] { ' ' }, 5, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 5) continue;
                var hexId = parts[0];
                if (!IsValidHexId(hexId)) continue;

                var logBody = parts[4];
                bool isError = logBody.Contains("ERROR", StringComparison.OrdinalIgnoreCase);

                if (!hexCounts.ContainsKey(hexId))
                    hexCounts[hexId] = (errorCount: 0, totalCount: 0);

                var counts = hexCounts[hexId];
                counts.totalCount++;
                if (isError) counts.errorCount++;
                hexCounts[hexId] = counts;
            }

            var ratios = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            double sum = 0;
            int countRatio = 0;

            foreach (var kvp in hexCounts)
            {
                var (errorCount, totalCount) = kvp.Value;
                double ratio = totalCount > 0 ? (double)errorCount / totalCount : 0;
                ratios[kvp.Key] = Math.Round(ratio, 4);
                sum += ratio;
                countRatio++;
            }

            double average = countRatio > 0 ? Math.Round(sum / countRatio, 4) : 0;

            return (ratios, average);
        }

       public async Task<List<FileData>> GetAllFilesDataAsync(string? logDirectoryPath = null)
{
    string directory = logDirectoryPath ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");
    var fileDataList = new List<FileData>();
    if (!Directory.Exists(directory)) return fileDataList;
    var logFiles = Directory.GetFiles(directory, "*.log");

    foreach (var filePath in logFiles)
    {
        var (sessionIds, errorCount) = ExtractSessionsAndErrors(filePath);
        var sessionCount = sessionIds.Count;
        double ratio = sessionCount > 0 ? (double)errorCount / sessionCount : 0;

        fileDataList.Add(new FileData
        {
            FileName = Path.GetFileName(filePath),
            ErrorRatio = Math.Round(ratio, 4),
            SessionCount = sessionCount,
            ErrorCount = errorCount
        });
    }
    return await Task.FromResult(fileDataList);
}

private (HashSet<string> sessions, int errorCount) ExtractSessionsAndErrors(string filePath)
{
    var lines = File.ReadAllLines(filePath);
    int errorCount = 0;
    HashSet<string> sessions = new();

    foreach (var line in lines)
    {
        if (line.Contains("ERROR", StringComparison.OrdinalIgnoreCase))
            errorCount++;

        var match = Regex.Match(line, @"sessionId=([a-zA-Z0-9\-]+)");
        if (match.Success)
            sessions.Add(match.Groups[1].Value);
    }

    return (sessions, errorCount);
}

    }
}
