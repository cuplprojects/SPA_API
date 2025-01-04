using SPA.Data;
using SPA.Models;

namespace SPA.Services
{
    public class Changelogger : IChangeLogger
    {
        private readonly FirstDbContext _firstDbContext;
        private readonly SecondDbContext _secondDbContext;
        private readonly DatabaseConnectionChecker _connectionChecker;

        public Changelogger(FirstDbContext firstDbContext, SecondDbContext secondDbContext, DatabaseConnectionChecker connectionChecker)
        {
            _firstDbContext = firstDbContext;
            _secondDbContext = secondDbContext;
            _connectionChecker = connectionChecker;
        }

        public void LogForDBSync(string Category,string Table ,string LogEntry, string WhichDatabase,int UserId)
        {
            ChangeLog log = new ChangeLog()
            {
                Category = Category,
                Table = Table,
                LogEntry = LogEntry,
                UpdatedBy = UserId

            };

            if (WhichDatabase == "Local")
            {

                _firstDbContext.ChangeLogs.Add(log);
                _firstDbContext.SaveChanges();
            }
            else
            {
                if (!_connectionChecker.IsOnlineDatabaseAvailable())
                {
                    throw new Exception("Connection Unavailable");
                }
                _secondDbContext.ChangeLogs.Add(log);
                _secondDbContext.SaveChanges();
            }

        }
    }
}
