namespace LogAnalysisApi
{
    public class ErrorRatioData
    {
        public float SessionCount { get; set; }
        public float ErrorCount { get; set; }
        public float ErrorRatio { get; set; }
    }

    public class ErrorRatioPrediction
    {
        public float Score { get; set; }
    }
}