public class FileData
{
    public required string FileName { get; set; }
    public double? ErrorRatio { get; set; } 
    public int SessionCount { get; set; }
    public int ErrorCount { get; set; }
}
