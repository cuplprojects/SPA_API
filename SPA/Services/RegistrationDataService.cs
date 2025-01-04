using SPA.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace SPA.Services
{
    public class RegistrationDataService
    {
        private readonly FirstDbContext _FirstDbContext;
        private readonly SecondDbContext _SecondDbContext;
        private readonly DatabaseConnectionChecker _connectionChecker;

        public RegistrationDataService(FirstDbContext context, SecondDbContext secondDbContext, DatabaseConnectionChecker connectionChecker)
        {
            _FirstDbContext = context;
            _SecondDbContext = secondDbContext;
            _connectionChecker = connectionChecker;
        }

        public async Task<List<RegistrationData>> GetRegistrationDataListAsync(string WhichDatabase, int ProjectId)
        {
            if (WhichDatabase == "Local")
            {
                return await _FirstDbContext.RegistrationDatas.Where(o => o.ProjectId == ProjectId).ToListAsync();
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    throw new Exception("Connection Unavailable");
                }
                return await _SecondDbContext.RegistrationDatas.Where(o => o.ProjectId == ProjectId).ToListAsync();
            }

        }

    }

}
