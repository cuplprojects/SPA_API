using SPA.Data;
using SPA.Models;

namespace SPA.Services
{
    public class LoggerService : ILoggerService
    {
        private readonly FirstDbContext _firstDbContext;
        private readonly SecondDbContext _secondDbContext;

        public LoggerService(FirstDbContext firstDbContext, SecondDbContext secondDbContext)
        {
            _firstDbContext = firstDbContext;
            _secondDbContext = secondDbContext;
        }

        public void LogEvent(string message, string category, int triggeredBy, string WhichDatabase)
        {
            var log = new EventLog
            {
                Event = message,
                EventTriggeredBy = triggeredBy,
                Category = category,
            };
            if (WhichDatabase == "Local")
            {
                _firstDbContext.EventLogs.Add(log);
                _firstDbContext.SaveChanges();
            }
            else
            {
                _secondDbContext.EventLogs.Add(log);
                _secondDbContext.SaveChanges();

            }
        }

        public void LogError(string error, string errormessage, string Controller, string WhichDatabase)
        {
            var log = new ErrorLog
            {
                Error = error,
                Message = errormessage,
                OccuranceSpace = Controller
            };

            if (WhichDatabase == "Local")
            {
                _firstDbContext.ErrorLogs.Add(log);
                _firstDbContext.SaveChanges();
            }
            else
            {
                _secondDbContext.ErrorLogs.Add(log);
                _secondDbContext.SaveChanges();

            }
        }
    }
}
