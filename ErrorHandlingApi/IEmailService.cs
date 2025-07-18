using LogAnalysisApi;

namespace LogAnalysisApi
{
    public interface IEmailService
    {
        System.Threading.Tasks.Task SendAlertEmailAsync(string subject, string body);
    }
}
