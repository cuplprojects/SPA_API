/*using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Linq;
using System.Reflection;

namespace SPA.Services
{

    public class SyncTables
    {
        private readonly IConfiguration _configuration;

        public SyncTables(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void InsertOrUpdate(string tableName, object entity, DbContext dbContext)
        {
            string query = GenerateInsertOrUpdateQuery(tableName, entity);
            var cmd = dbContext.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = query;

            AddParameters(cmd, entity);

            // Log the query and parameters for debugging
            Console.WriteLine($"Executing SQL Query: {cmd.CommandText}");
            foreach (DbParameter param in cmd.Parameters)
            {
                Console.WriteLine($"Parameter: {param.ParameterName}, Value: {param.Value}");
            }

            try
            {
                dbContext.Database.OpenConnection();
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while executing the query: {ex.Message}");
            }
            finally
            {
                dbContext.Database.CloseConnection();
            }
        }

        public void Delete(string tableName, object entity, DbContext dbContext)
        {
            var keyProperty = entity.GetType().GetProperties().FirstOrDefault(prop => Attribute.IsDefined(prop, typeof(KeyAttribute)));
            if (keyProperty == null)
            {
                throw new Exception($"No key property found on entity type {entity.GetType().Name}");
            }

            var keyName = keyProperty.Name;
            var keyValue = keyProperty.GetValue(entity);

            string query = $"DELETE FROM {tableName} WHERE {keyName} = @{keyName}";
            var cmd = dbContext.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = query;

            var parameter = cmd.CreateParameter();
            parameter.ParameterName = $"@{keyName}";
            parameter.Value = keyValue;
            cmd.Parameters.Add(parameter);

            // Log the query and parameters for debugging
            Console.WriteLine($"Executing SQL Query: {cmd.CommandText}");
            foreach (DbParameter param in cmd.Parameters)
            {
                Console.WriteLine($"Parameter: {param.ParameterName}, Value: {param.Value}");
            }

            try
            {
                dbContext.Database.OpenConnection();
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while executing the query: {ex.Message}");
            }
            finally
            {
                dbContext.Database.CloseConnection();
            }
        }

        private void AddParameters(DbCommand cmd, object entity)
        {
            foreach (var property in entity.GetType().GetProperties())
            {
                if (Attribute.IsDefined(property, typeof(NotMappedAttribute))) continue; // Skip NotMapped properties

                var parameter = cmd.CreateParameter();
                parameter.ParameterName = $"@{property.Name}";
                var value = property.GetValue(entity);

                if (value != null)
                {
                    if (IsJson(value.ToString()))
                    {
                        parameter.Value = value.ToString(); // Convert JSON to string
                    }
                    else if (value is IEnumerable<object>)
                    {
                        parameter.Value = JsonConvert.SerializeObject(value); // Convert list to JSON string
                    }
                    else
                    {
                        parameter.Value = value;
                    }
                }
                else
                {
                    parameter.Value = DBNull.Value;
                }

                cmd.Parameters.Add(parameter);
            }
        }

        private bool IsJson(string input)
        {
            input = input.Trim();
            return (input.StartsWith("{") && input.EndsWith("}")) || (input.StartsWith("[") && input.EndsWith("]"));
        }

        private string GenerateInsertOrUpdateQuery(string tableName, object entity)
        {
            var properties = entity.GetType().GetProperties()
                .Where(p => !Attribute.IsDefined(p, typeof(NotMappedAttribute))); // Exclude NotMapped properties
            var columns = string.Join(", ", properties.Select(p => p.Name));
            var parameters = string.Join(", ", properties.Select(p => $"@{p.Name}"));
            var updates = string.Join(", ", properties.Select(p => $"{p.Name} = @{p.Name}"));

            return $@"INSERT INTO {tableName} ({columns}) 
                  VALUES ({parameters}) 
                  ON DUPLICATE KEY UPDATE {updates}";
        }
    }

}
*/
/*

using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Linq;
using System.Reflection;

namespace SPA.Services
{
    public class SyncTables
    {
        private readonly IConfiguration _configuration;

        public SyncTables(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void InsertOrUpdate(string tableName, object entity, DbContext dbContext)
        {
            string query = GenerateInsertOrUpdateQuery(tableName, entity);
            var cmd = dbContext.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = query;

            AddParameters(cmd, entity);

            // Log the query and parameters for debugging
            Console.WriteLine($"Executing SQL Query: {cmd.CommandText}");
            foreach (DbParameter param in cmd.Parameters)
            {
                Console.WriteLine($"Parameter: {param.ParameterName}, Value: {param.Value}");
            }

            try
            {
                dbContext.Database.OpenConnection();
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while executing the query: {ex.Message}");
            }
            finally
            {
                dbContext.Database.CloseConnection();
            }
        }

        public void Delete(string tableName, object entity, DbContext dbContext)
        {
            var keyProperty = entity.GetType().GetProperties().FirstOrDefault(prop => Attribute.IsDefined(prop, typeof(KeyAttribute)));
            if (keyProperty == null)
            {
                throw new Exception($"No key property found on entity type {entity.GetType().Name}");
            }

            var keyName = keyProperty.Name;
            var keyValue = keyProperty.GetValue(entity);

            string query = $"DELETE FROM {tableName} WHERE {keyName} = @{keyName}";
            var cmd = dbContext.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = query;

            var parameter = cmd.CreateParameter();
            parameter.ParameterName = $"@{keyName}";
            parameter.Value = keyValue;
            cmd.Parameters.Add(parameter);

            // Log the query and parameters for debugging
            Console.WriteLine($"Executing SQL Query: {cmd.CommandText}");
            foreach (DbParameter param in cmd.Parameters)
            {
                Console.WriteLine($"Parameter: {param.ParameterName}, Value: {param.Value}");
            }

            try
            {
                dbContext.Database.OpenConnection();
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while executing the query: {ex.Message}");
            }
            finally
            {
                dbContext.Database.CloseConnection();
            }
        }

        private void AddParameters(DbCommand cmd, object entity)
        {
            foreach (var property in entity.GetType().GetProperties())
            {
                if (Attribute.IsDefined(property, typeof(NotMappedAttribute))) continue; // Skip NotMapped properties

                var parameter = cmd.CreateParameter();
                parameter.ParameterName = $"@{property.Name}";
                var value = property.GetValue(entity);

                if (value != null)
                {
                    if (value is string || IsJson(value.ToString()))
                    {
                        parameter.Value = value.ToString(); // Convert JSON or string to string
                    }
                    else if (value is IEnumerable<int>) // Handle List<int>
                    {
                        parameter.Value = JsonConvert.SerializeObject(value); // Convert list to JSON string
                    }
                    else
                    {
                        parameter.Value = value;
                    }
                }
                else
                {
                    parameter.Value = DBNull.Value;
                }

                cmd.Parameters.Add(parameter);
            }
        }

        private bool IsJson(string input)
        {
            input = input.Trim();
            return (input.StartsWith("{") && input.EndsWith("}")) || (input.StartsWith("[") && input.EndsWith("]"));
        }

        private string GenerateInsertOrUpdateQuery(string tableName, object entity)
        {
            var properties = entity.GetType().GetProperties()
                .Where(p => !Attribute.IsDefined(p, typeof(NotMappedAttribute))); // Exclude NotMapped properties
            var columns = string.Join(", ", properties.Select(p => p.Name));
            var parameters = string.Join(", ", properties.Select(p => $"@{p.Name}"));
            var updates = string.Join(", ", properties.Select(p => $"{p.Name} = @{p.Name}"));

            return $@"INSERT INTO {tableName} ({columns}) 
                  VALUES ({parameters}) 
                  ON DUPLICATE KEY UPDATE {updates}";
        }
    }
}
*/

using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Linq;
using System.Reflection;

namespace SPA.Services
{
    public class SyncTables
    {
        private readonly IConfiguration _configuration;

        public SyncTables(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void InsertOrUpdate(string tableName, object entity, DbContext dbContext, int rolePriority)
        {
            string query = GenerateInsertOrUpdateQuery(tableName, entity);
            var cmd = dbContext.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = query;

            AddParameters(cmd, entity);

            // Log the query and parameters for debugging
            Console.WriteLine($"Executing SQL Query: {cmd.CommandText}");
            foreach (DbParameter param in cmd.Parameters)
            {
                Console.WriteLine($"Parameter: {param.ParameterName}, Value: {param.Value}");
            }

            try
            {
                dbContext.Database.OpenConnection();

                // Use reflection to get the DbSet<TEntity>
                var dbSetMethod = typeof(DbContext).GetMethod(nameof(DbContext.Set), Type.EmptyTypes);
                var dbSet = dbSetMethod.MakeGenericMethod(entity.GetType()).Invoke(dbContext, null);

                var keyValues = GetKeyValues(entity);
                var dbSetType = typeof(DbSet<>).MakeGenericType(entity.GetType());
                var findMethod = dbSetType.GetMethod(nameof(DbSet<object>.Find), new[] { typeof(object[]) });
                var existingEntity = findMethod.Invoke(dbSet, new object[] { keyValues });

                if (existingEntity != null)
                {
                    var existingRolePriority = GetUserRolePriority(dbContext, GetUpdatedBy(existingEntity));
                    if (rolePriority < existingRolePriority) // Lower number means higher priority
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
                else
                {
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while executing the query: {ex.Message}");
            }
            finally
            {
                dbContext.Database.CloseConnection();
            }
        }

        public void Delete(string tableName, object entity, DbContext dbContext)
        {
            var keyProperty = entity.GetType().GetProperties().FirstOrDefault(prop => Attribute.IsDefined(prop, typeof(KeyAttribute)));
            if (keyProperty == null)
            {
                throw new Exception($"No key property found on entity type {entity.GetType().Name}");
            }

            var keyName = keyProperty.Name;
            var keyValue = keyProperty.GetValue(entity);

            string query = $"DELETE FROM {tableName} WHERE {keyName} = @{keyName}";
            var cmd = dbContext.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = query;

            var parameter = cmd.CreateParameter();
            parameter.ParameterName = $"@{keyName}";
            parameter.Value = keyValue;
            cmd.Parameters.Add(parameter);

            // Log the query and parameters for debugging
            Console.WriteLine($"Executing SQL Query: {cmd.CommandText}");
            foreach (DbParameter param in cmd.Parameters)
            {
                Console.WriteLine($"Parameter: {param.ParameterName}, Value: {param.Value}");
            }

            try
            {
                dbContext.Database.OpenConnection();
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while executing the query: {ex.Message}");
            }
            finally
            {
                dbContext.Database.CloseConnection();
            }
        }

        private void AddParameters(DbCommand cmd, object entity)
        {
            foreach (var property in entity.GetType().GetProperties())
            {
                if (Attribute.IsDefined(property, typeof(NotMappedAttribute))) continue; // Skip NotMapped properties

                var parameter = cmd.CreateParameter();
                parameter.ParameterName = $"@{property.Name}";
                var value = property.GetValue(entity);

                if (value != null)
                {
                    if (value is string || IsJson(value.ToString()))
                    {
                        parameter.Value = value.ToString(); // Convert JSON or string to string
                    }
                    else if (value is IEnumerable<int>) // Handle List<int>
                    {
                        parameter.Value = JsonConvert.SerializeObject(value); // Convert list to JSON string
                    }
                    else
                    {
                        parameter.Value = value;
                    }
                }
                else
                {
                    parameter.Value = DBNull.Value;
                }

                cmd.Parameters.Add(parameter);
            }
        }

        private bool IsJson(string input)
        {
            input = input.Trim();
            return (input.StartsWith("{") && input.EndsWith("}")) || (input.StartsWith("[") && input.EndsWith("]"));
        }

        private string GenerateInsertOrUpdateQuery(string tableName, object entity)
        {
            var properties = entity.GetType().GetProperties()
                .Where(p => !Attribute.IsDefined(p, typeof(NotMappedAttribute))); // Exclude NotMapped properties
            var columns = string.Join(", ", properties.Select(p => p.Name));
            var parameters = string.Join(", ", properties.Select(p => $"@{p.Name}"));
            var updates = string.Join(", ", properties.Select(p => $"{p.Name} = @{p.Name}"));

            return $@"INSERT INTO {tableName} ({columns}) 
                  VALUES ({parameters}) 
                  ON DUPLICATE KEY UPDATE {updates}";
        }

        private object[] GetKeyValues(object entity)
        {
            var keyProperties = entity.GetType().GetProperties().Where(prop => Attribute.IsDefined(prop, typeof(KeyAttribute)));
            return keyProperties.Select(prop => prop.GetValue(entity)).ToArray();
        }

        private int GetUpdatedBy(object entity)
        {
            var updatedByProperty = entity.GetType().GetProperty("UpdatedBy");
            return updatedByProperty != null ? (int)updatedByProperty.GetValue(entity) : 0;
        }

        private int GetUserRolePriority(DbContext dbContext, int userId)
        {
            var userDbSet = dbContext.Set<User>();
            var user = userDbSet.Find(userId);
            return user?.RoleId ?? int.MaxValue;
        }
    }
}
