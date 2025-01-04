using Microsoft.EntityFrameworkCore;
using SPA.Data;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using System.Threading.Tasks;

namespace SPA.Services
{
    public class DatabaseConnectionChecker
    {
        private readonly SecondDbContext _secondDbContext;

        public DatabaseConnectionChecker(SecondDbContext secondDbContext)
        {
            _secondDbContext = secondDbContext;
        }

        public async Task<bool> IsOnlineDatabaseAvailableAsync()
        {
            var connectionString = "server=a2nlmysql31plsk.secureserver.net;port=3306;database=CuplTest3;user=test3;password=t@Vz9798r";
            using (var connection = new MySqlConnection(connectionString))
            {
                try
                {
                    await connection.OpenAsync();
                    await connection.CloseAsync();
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }
        public bool IsOnlineDatabaseAvailable()
        {
            var connectionString = "server=a2nlmysql31plsk.secureserver.net;port=3306;database=CuplTest3;user=test3;password=t@Vz9798r";
            using (var connection = new MySqlConnection(connectionString))
            {
                try
                {
                    connection.Open();
                    connection.Close();
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }
    }
}
