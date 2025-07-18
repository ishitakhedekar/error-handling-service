namespace LogAnalysisApi
{
    public class EmailSettings
    {
        public string SmtpServer { get; set; } = string.Empty;
        public int SmtpPort { get; set; }
        public string FromEmail { get; set; } = string.Empty;
        public string FromPassword { get; set; } = string.Empty;
    }
}
