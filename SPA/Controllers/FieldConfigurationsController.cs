using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SPA;
using SPA.Data;
using SPA.Services;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SPA.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class FieldConfigurationsController : ControllerBase
    {
        private readonly FirstDbContext _firstDbContext;
        private readonly SecondDbContext _secondDbContext;
        private readonly IChangeLogger _changeLogger;
        private readonly ISecurityService _securityService;
        private readonly DatabaseConnectionChecker _connectionChecker;
        private readonly ILoggerService _logger;


        public FieldConfigurationsController(FirstDbContext firstDbContext, SecondDbContext secondDbContext, IChangeLogger changeLogger, ISecurityService securityService, DatabaseConnectionChecker connectionChecker, ILoggerService logger)
        {
            _firstDbContext = firstDbContext;
            _secondDbContext = secondDbContext;
            _changeLogger = changeLogger;
            _securityService = securityService;
            _connectionChecker = connectionChecker;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<FieldConfig>>> GetFieldsConfig(string WhichDatabase)
        {
            if (WhichDatabase == "Local")
            {
                return await _firstDbContext.FieldConfigs.ToListAsync();
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                return await _secondDbContext.FieldConfigs.ToListAsync();
            }

        }

        [HttpGet("GetByProjectId/{id}")]
        public async Task<ActionResult<string>> GetFieldsConfigbyProjectId(int id, string WhichDatabase)
        {
            // Retrieve the list of FieldConfig objects based on the project ID
            List<FieldConfig> fieldConfigs;
            if (WhichDatabase == "Local")
            {
                fieldConfigs = await _firstDbContext.FieldConfigs.Where(f => f.ProjectId == id).ToListAsync();
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                fieldConfigs = await _secondDbContext.FieldConfigs.Where(f => f.ProjectId == id).ToListAsync();
            }

            // Check if no FieldConfig objects are found
            if (fieldConfigs == null || fieldConfigs.Count == 0)
            {
                return NotFound("No field configurations found for the specified project ID.");
            }

            // Serialize the list of FieldConfig objects to a JSON string
            string fieldConfigsJson = JsonConvert.SerializeObject(fieldConfigs);

            // Encrypt the JSON string
            string encryptedJson = _securityService.Encrypt(fieldConfigsJson);

            // Return the encrypted JSON string
            return Ok(encryptedJson);
        }



        [HttpGet("{id}")]
        public async Task<ActionResult<FieldConfig>> GetFieldConfiguration(int id, string WhichDatabase)
        {
            if (WhichDatabase == "Local")
            {
                var fieldConfiguration = await _firstDbContext.FieldConfigs.FindAsync(id);

                if (fieldConfiguration == null)
                {
                    return NotFound();
                }

                return fieldConfiguration;
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                var fieldConfiguration = await _secondDbContext.FieldConfigs.FindAsync(id);

                if (fieldConfiguration == null)
                {
                    return NotFound();
                }

                return fieldConfiguration;
            }
        }


        [HttpPut("{id}")]
        public async Task<IActionResult> PutFieldConfiguration(int id, Cyphertext cyphertext, string WhichDatabase)
        {
            // Decrypt the cyphertext
            string decryptedJson = _securityService.Decrypt(cyphertext.cyphertextt);

            // Deserialize the decrypted JSON into the FieldConfig object
            var fieldConfiguration = JsonConvert.DeserializeObject<FieldConfig>(decryptedJson);

            // Check if the provided id matches the id in the fieldConfiguration object
            if (id != fieldConfiguration.FieldConfigurationId)
            {
                return BadRequest();
            }

            // Set the state of the entity to Modified
            if (WhichDatabase == "Local")
            {
                _firstDbContext.Entry(fieldConfiguration).State = EntityState.Modified;
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                _secondDbContext.Entry(fieldConfiguration).State = EntityState.Modified;
            }

            try
            {
                // Save changes to the appropriate database context
                if (WhichDatabase == "Local")
                {
                    await _firstDbContext.SaveChangesAsync();
                }
                else
                {
                    await _secondDbContext.SaveChangesAsync();
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                // Check if the FieldConfig exists before throwing an exception
                if (!FieldConfigurationExists(id, WhichDatabase))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {

                _logger.LogEvent($"Field Configuration {id} Updated ", "Field Configuration", userId, WhichDatabase);
            }

            // Serialize the updated FieldConfig object and log the update

            return NoContent();
        }


        /*[HttpPost]
        public async Task<ActionResult<FieldConfig>> PostFieldConfiguration(FieldConfig fieldConfiguration, string WhichDatabase)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest("Wrong input");
            }

            if (WhichDatabase == "Local")
            {
                _firstDbContext.FieldConfigs.Add(fieldConfiguration);

            }
            else
            {
                _secondDbContext.FieldConfigs.Add(fieldConfiguration);
            }

            try
            {
                if (WhichDatabase == "Local")
                {
                    await _firstDbContext.SaveChangesAsync();
                }
                else
                {
                    await _secondDbContext.SaveChangesAsync();
                }


            }
            catch (DbUpdateException)
            {
                if (FieldConfigurationExists(fieldConfiguration.FieldConfigurationId, WhichDatabase))
                {
                    return Conflict();
                }
                else
                {
                    throw;
                }
            }
            string FieldConfigJson = JsonConvert.SerializeObject(fieldConfiguration);
            string jsonUpdate = FieldConfigJson.Replace("\\\"", "'").Trim();
            _changeLogger.LogForDBSync("Update", "FieldConfigs", jsonUpdate, WhichDatabase);

            return CreatedAtAction("GetFieldConfiguration", new { id = fieldConfiguration.FieldConfigurationId }, fieldConfiguration);
        }*/

        [HttpPost]
        public async Task<ActionResult<FieldConfig>> PostFieldConfiguration(Cyphertext cyphertext, string WhichDatabase)
        {
            // Decrypt the cyphertext
            string decryptedJson = _securityService.Decrypt(cyphertext.cyphertextt);

            // Deserialize the decrypted JSON into the FieldConfig object
            var fieldConfiguration = JsonConvert.DeserializeObject<FieldConfig>(decryptedJson);
            fieldConfiguration.FieldConfigurationId = GetNextFieldConfigId();

            if (!ModelState.IsValid)
            {
                return BadRequest("Wrong input");
            }

            if (WhichDatabase == "Local")
            {
                _firstDbContext.FieldConfigs.Add(fieldConfiguration);
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                _secondDbContext.FieldConfigs.Add(fieldConfiguration);
            }

            try
            {
                if (WhichDatabase == "Local")
                {
                    await _firstDbContext.SaveChangesAsync();
                }
                else
                {
                    await _secondDbContext.SaveChangesAsync();
                }
            }
            catch (DbUpdateException)
            {
                if (FieldConfigurationExists(fieldConfiguration.FieldConfigurationId, WhichDatabase))
                {
                    return Conflict();
                }
                else
                {
                    throw;
                }
            }

            var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                _logger.LogEvent($"Field Configuration {fieldConfiguration.FieldConfigurationId} Added", "Field Configuration", userId, WhichDatabase);
            }

            return CreatedAtAction("GetFieldConfiguration", new { id = fieldConfiguration.FieldConfigurationId }, fieldConfiguration);
        }


        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteFieldConfiguration(int id, string WhichDatabase)
        {
            if (WhichDatabase == "Local")
            {
                var fieldConfiguration = await _firstDbContext.FieldConfigs.FindAsync(id);
                if (fieldConfiguration == null)
                {
                    return NotFound();
                }

                _firstDbContext.FieldConfigs.Remove(fieldConfiguration);
                await _firstDbContext.SaveChangesAsync();
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                var fieldConfiguration = await _secondDbContext.FieldConfigs.FindAsync(id);
                if (fieldConfiguration == null)
                {
                    return NotFound();
                }

                _secondDbContext.FieldConfigs.Remove(fieldConfiguration);
                await _secondDbContext.SaveChangesAsync();
            }

            var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                _logger.LogEvent($"Field Configuration {id} Deleted", "Field Configuration", userId, WhichDatabase);
            }


            return NoContent();
        }

        private bool FieldConfigurationExists(int id, string WhichDatabase)
        {
            if (WhichDatabase == "Local")
            {
                return _firstDbContext.FieldConfigs.Any(e => e.FieldConfigurationId == id);
            }
            else
            {
                if (_connectionChecker.IsOnlineDatabaseAvailable())
                {
                    return _secondDbContext.FieldConfigs.Any(e => e.FieldConfigurationId == id);
                }
                return false;
            }


        }

        private int GetNextFieldConfigId()
        {
            int maxResIdSecond = 0;
            int maxResIdLocal = _firstDbContext.FieldConfigs.Any() ? _firstDbContext.FieldConfigs.Max(u => u.FieldConfigurationId) : 0;
            try
            {
                if (_connectionChecker.IsOnlineDatabaseAvailable())
                {
                    maxResIdSecond = _secondDbContext.FieldConfigs.Any() ? _secondDbContext.FieldConfigs.Max(u => u.FieldConfigurationId) : 0;
                }
                else
                {
                    maxResIdSecond = 0;
                }
            }
            catch (Exception ex)
            {
                maxResIdSecond = 0;
            }


            int maxResId = Math.Max(maxResIdLocal, maxResIdSecond);

            return maxResId + 1;
        }
    }
}
