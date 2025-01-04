namespace SPA.Services
{
    public interface IChangeLogger
    {
        void LogForDBSync(string Category,string Table,string LogEntry, string WhichDatabase,int UserId);
    }
}
