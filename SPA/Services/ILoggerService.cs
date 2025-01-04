namespace SPA.Services
{
    public interface ILoggerService
    {
        void LogEvent(string message, string category, int triggeredBy, string WhichDatabase);
        void LogError(string error, string errorMsg, string controller, string WhichDatabase);
    }
}
