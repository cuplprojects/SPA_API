using Microsoft.EntityFrameworkCore;
using SPA.Data;

namespace SPA.Services
{
    public class DatabaseSynchronizer
    {
        private readonly DbContext _sourceDbContext;
        private readonly DbContext _targetDbContext;

        public DatabaseSynchronizer(DbContext sourceDbContext, DbContext targetDbContext)
        {
            _sourceDbContext = sourceDbContext;
            _targetDbContext = targetDbContext;
        }

        public void SynchronizeAllTables()
        {
            var sourceDbSets = _sourceDbContext.GetType().GetProperties()
                .Where(p => p.PropertyType.IsGenericType &&
                            p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
                .ToList();

            foreach (var dbSetProperty in sourceDbSets)
            {
                var entityType = dbSetProperty.PropertyType.GetGenericArguments().First();

                var setMethod = typeof(DbContext).GetMethod("Set", Type.EmptyTypes).MakeGenericMethod(entityType);

                var sourceDbSet = setMethod.Invoke(_sourceDbContext, null);
                var targetDbSet = setMethod.Invoke(_targetDbContext, null);

                var sourceDbData = ((IQueryable<object>)sourceDbSet).ToList();
                var targetDbData = ((IQueryable<object>)targetDbSet).ToList();

                // Synchronize source to target
                SynchronizeDbSets(sourceDbData, targetDbData, entityType, targetDbSet, _targetDbContext);

                // Synchronize target to source
                SynchronizeDbSets(targetDbData, sourceDbData, entityType, sourceDbSet, _sourceDbContext);
            }
        }

        private void SynchronizeDbSets(List<object> sourceData, List<object> targetData, Type entityType, object targetDbSet, DbContext targetDbContext)
        {
            foreach (var entity in sourceData)
            {
                var key = GetEntityKey(entityType, entity);
                var targetEntity = targetData.FirstOrDefault(e => GetEntityKey(entityType, e).Equals(key));

                if (targetEntity == null)
                {
                    // Add new entity to the target
                    try
                    {
                        var addMethod = targetDbSet.GetType().GetMethod("Add");
                        addMethod.Invoke(targetDbSet, new[] { entity });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error adding entity to target database: {ex.InnerException?.Message ?? ex.Message}");
                    }
                }
                else
                {
                    // Update existing entity in the target if there are differences
                    if (!AreEntitiesEqual(entity, targetEntity))
                    {
                        try
                        {
                            targetDbContext.Entry(targetEntity).CurrentValues.SetValues(entity);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error updating entity in target database: {ex.InnerException?.Message ?? ex.Message}");
                        }
                    }
                }
            }

            try
            {
                targetDbContext.SaveChanges();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving changes to target database: {ex.Message}");
            }
        }

        private object GetEntityKey(Type entityType, object entity)
        {
            // Assuming the key property is named "Id" for simplicity
            var keyProperty = entityType.GetProperties().FirstOrDefault(p => p.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase));
            return keyProperty?.GetValue(entity);
        }

        private bool AreEntitiesEqual(object entity1, object entity2)
        {
            var type = entity1.GetType();
            foreach (var property in type.GetProperties().Where(p => p.CanRead))
            {
                var value1 = property.GetValue(entity1);
                var value2 = property.GetValue(entity2);

                if (value1 == null && value2 != null || value1 != null && !value1.Equals(value2))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
