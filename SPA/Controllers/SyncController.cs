/*using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SPA.Services;
using System;
using SPA.Data;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using SPA.Models;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Authorization;

namespace SPA.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SyncController : ControllerBase
    {
        private readonly FirstDbContext _firstDbContext;
        private readonly SecondDbContext _secondDbContext;
        private readonly IConfiguration _configuration;

        public SyncController(FirstDbContext firstDbContext, SecondDbContext secondDbContext, IConfiguration configuration)
        {
            _firstDbContext = firstDbContext;
            _secondDbContext = secondDbContext;
            _configuration = configuration;
        }

        [HttpPost("SyncLogs")]
        public async Task<IActionResult> SyncDatabase()
        {
            try
            {
                await SyncChangeLogs(_firstDbContext, _secondDbContext);
                await SyncChangeLogs(_secondDbContext, _firstDbContext);

                return Ok("Database Sync Successful");
            }
            catch (Exception ex)
            {
                // Log the exception
                Console.WriteLine($"An error occurred: {ex.Message}");
                return BadRequest("An error occurred while syncing databases");
            }
        }

        private async Task SyncChangeLogs(DbContext sourceDbContext, DbContext targetDbContext)
        {
            var itemsToSync = await sourceDbContext.Set<ChangeLog>().Where(u => !u.IsSynced).ToListAsync();

            foreach (var item in itemsToSync)
            {
                var entityType = GetEntityType(item.Table);
                if (entityType == null)
                {
                    Console.WriteLine($"Entity type {item.Table} not found.");
                    continue;
                }

                var entity = JsonConvert.DeserializeObject(item.LogEntry, entityType);

                SyncTables syncTables = new SyncTables(_configuration);

                if (item.Category == "Delete")
                {
                    syncTables.Delete(item.Table, entity, targetDbContext);
                }
                else
                {
                    // Convert nested properties to JSON
                    ConvertNestedPropertiesToJson(entity);
                    syncTables.InsertOrUpdate(item.Table, entity, targetDbContext);
                }

                item.IsSynced = true; // Mark the item as synced
            }

            await targetDbContext.SaveChangesAsync();
            await sourceDbContext.SaveChangesAsync();
        }

        private void ConvertNestedPropertiesToJson(object entity)
        {
            var properties = entity.GetType().GetProperties();
            foreach (var property in properties)
            {
                if (Attribute.IsDefined(property, typeof(NotMappedAttribute))) continue; // Skip NotMapped properties

                var value = property.GetValue(entity);
                if (value is IEnumerable<object>)
                {
                    var jsonProperty = properties.FirstOrDefault(p => p.Name == $"{property.Name}Json");
                    if (jsonProperty != null)
                    {
                        jsonProperty.SetValue(entity, JsonConvert.SerializeObject(value));
                    }
                }
            }
        }

        private Type GetEntityType(string tableName)
        {
            // Assuming all entity types are in the same namespace and assembly
            var assembly = Assembly.GetExecutingAssembly();

            // Adjust the table name to match the entity type name, only trimming one 's' if it exists at the end
            var entityTypeName = tableName.EndsWith("s") ? tableName.Substring(0, tableName.Length - 1) : tableName;

            var entityType = assembly.GetTypes().FirstOrDefault(t => t.Name == entityTypeName);
            return entityType;
        }

    }
}
*/

/*using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SPA.Services;
using System;
using SPA.Data;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using SPA.Models;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Authorization;

namespace SPA.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SyncController : ControllerBase
    {
        private readonly FirstDbContext _firstDbContext;
        private readonly SecondDbContext _secondDbContext;
        private readonly IConfiguration _configuration;

        public SyncController(FirstDbContext firstDbContext, SecondDbContext secondDbContext, IConfiguration configuration)
        {
            _firstDbContext = firstDbContext;
            _secondDbContext = secondDbContext;
            _configuration = configuration;
        }

        [HttpPost("SyncLogs")]
        public async Task<IActionResult> SyncDatabase()
        {
            try
            {
                await SyncChangeLogs(_firstDbContext, _secondDbContext);
                await SyncChangeLogs(_secondDbContext, _firstDbContext);

                return Ok("Database Sync Successful");
            }
            catch (Exception ex)
            {
                // Log the exception
                Console.WriteLine($"An error occurred: {ex.Message}");
                return BadRequest("An error occurred while syncing databases");
            }
        }

        private async Task SyncChangeLogs(DbContext sourceDbContext, DbContext targetDbContext)
        {
            var itemsToSync = await sourceDbContext.Set<ChangeLog>()
                .Where(u => !u.IsSynced)
                .Join(sourceDbContext.Set<User>(),
                      changeLog => changeLog.UpdatedBy,
                      user => user.UserId,
                      (changeLog, user) => new { changeLog, user.RoleId })
                .ToListAsync();

            foreach (var item in itemsToSync)
            {
                var entityType = GetEntityType(item.changeLog.Table);
                if (entityType == null)
                {
                    Console.WriteLine($"Entity type {item.changeLog.Table} not found.");
                    continue;
                }

                var entity = JsonConvert.DeserializeObject(item.changeLog.LogEntry, entityType);

                SyncTables syncTables = new SyncTables(_configuration);

                if (item.changeLog.Category == "Delete")
                {
                    syncTables.Delete(item.changeLog.Table, entity, targetDbContext);
                }
                else
                {
                    // Convert nested properties to JSON
                    ConvertNestedPropertiesToJson(entity);
                    syncTables.InsertOrUpdate(item.changeLog.Table, entity, targetDbContext, item.RoleId);
                }

                item.changeLog.IsSynced = true; // Mark the item as synced
            }

            await targetDbContext.SaveChangesAsync();
            await sourceDbContext.SaveChangesAsync();
        }

        private void ConvertNestedPropertiesToJson(object entity)
        {
            var properties = entity.GetType().GetProperties();
            foreach (var property in properties)
            {
                if (Attribute.IsDefined(property, typeof(NotMappedAttribute))) continue; // Skip NotMapped properties

                var value = property.GetValue(entity);
                if (value is IEnumerable<object>)
                {
                    var jsonProperty = properties.FirstOrDefault(p => p.Name == $"{property.Name}Json");
                    if (jsonProperty != null)
                    {
                        jsonProperty.SetValue(entity, JsonConvert.SerializeObject(value));
                    }
                }
            }
        }

        private Type GetEntityType(string tableName)
        {
            // Assuming all entity types are in the same namespace and assembly
            var assembly = Assembly.GetExecutingAssembly();

            // Adjust the table name to match the entity type name, only trimming one 's' if it exists at the end
            var entityTypeName = tableName.EndsWith("s") ? tableName.Substring(0, tableName.Length - 1) : tableName;

            var entityType = assembly.GetTypes().FirstOrDefault(t => t.Name == entityTypeName);
            return entityType;
        }
    }
}
*/

/*using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SPA.Services;
using System;
using SPA.Data;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using SPA.Models;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Authorization;

namespace SPA.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SyncController : ControllerBase
    {
        private readonly FirstDbContext _firstDbContext;
        private readonly SecondDbContext _secondDbContext;

        public SyncController(FirstDbContext firstDbContext, SecondDbContext secondDbContext)
        {
            _firstDbContext = firstDbContext;
            _secondDbContext = secondDbContext;
        }

        [HttpPost("SyncLogs")]
        public IActionResult SyncAllTables()
        {
            try
            {
                var synchronizer = new DatabaseSynchronizer(_firstDbContext, _secondDbContext);
                synchronizer.SynchronizeAllTables();

                // Synchronize in the opposite direction
                var reverseSynchronizer = new DatabaseSynchronizer(_secondDbContext, _firstDbContext);
                reverseSynchronizer.SynchronizeAllTables();

                return Ok("All tables synchronized successfully.");
            }
            catch (Exception ex)
            {
                // Log the exception
                Console.WriteLine($"An error occurred: {ex.Message}");
                return BadRequest("An error occurred while syncing the databases.");
            }
        }


        // The rest of your controller methods remain the same
    }
}
*/

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SPA.Services;
using System;
using SPA.Data;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using SPA.Models;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Generic;

namespace SPA.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SyncController : ControllerBase
    {
        private readonly FirstDbContext _firstDbContext;
        private readonly SecondDbContext _secondDbContext;

        public SyncController(FirstDbContext firstDbContext, SecondDbContext secondDbContext)
        {
            _firstDbContext = firstDbContext;
            _secondDbContext = secondDbContext;
        }

        [HttpPost("SyncLogs")]
        public IActionResult SyncAllTables()
        {
            try
            {
                // List of global tables to be synced entirely
                var globalTables = new List<string>
                {
                    "Fields",
                    "Projects",
                    "Users",
                    "UserAuths",
                    "Roles"
                };

                // Initialize the synchronizer with global tables
                var synchronizer = new DatabaseSynchronizer(_firstDbContext, _secondDbContext);
                synchronizer.SynchronizeAllTables();

                // Synchronize in the opposite direction
                var reverseSynchronizer = new DatabaseSynchronizer(_secondDbContext, _firstDbContext);
                reverseSynchronizer.SynchronizeAllTables();

                return Ok("All tables synchronized successfully.");
            }
            catch (Exception ex)
            {
                // Log the exception
                Console.WriteLine($"An error occurred: {ex.Message}");
                return BadRequest("An error occurred while syncing the databases.");
            }
        }

        // The rest of your controller methods remain the same
    }
}
