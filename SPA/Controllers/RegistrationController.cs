using Microsoft.AspNetCore.Mvc;
using SPA.Data;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Microsoft.EntityFrameworkCore;
using SPA.Services;
using SPA.Models;
using SPA.Models.NonDBModels;
using System.Text.Json;
using System.Drawing.Printing;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.CodeAnalysis;

namespace SPA.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class RegistrationController : ControllerBase
    {
        private readonly FirstDbContext _firstDbContext;
        private readonly SecondDbContext _secondDbContext;
        private readonly IChangeLogger _changeLogger;
        private readonly ISecurityService _securityService;
        private readonly DatabaseConnectionChecker _connectionChecker;
        private readonly ILoggerService _logger;

        public RegistrationController(
            FirstDbContext firstDbContext,
            SecondDbContext secondDbContext,
            IChangeLogger changeLogger,
            ISecurityService securityService,
            DatabaseConnectionChecker connectionChecker,
            ILoggerService logger)
        {
            _firstDbContext = firstDbContext;
            _secondDbContext = secondDbContext;
            _changeLogger = changeLogger;
            _securityService = securityService;
            _connectionChecker = connectionChecker;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> Post(Cyphertext cyphertext, string WhichDatabase, int ProjectId)
        {
            // Decrypt the cyphertext
            string decryptedJson = _securityService.Decrypt(cyphertext.cyphertextt);

            // Deserialize the decrypted JSON into the List<Dictionary<string, string>> object
            List<Dictionary<string, string>> mappedData = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(decryptedJson);

            // Validate the list of dictionaries
            if (mappedData == null || mappedData.Count == 0)
            {
                return BadRequest(new { message = "No data received" });
            }

            try
            {
                if (WhichDatabase == "Local")
                {
                    await SaveToDatabase(_firstDbContext, mappedData, ProjectId, WhichDatabase);
                }
                else
                {
                    if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                    {
                        return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                    }
                    await SaveToDatabase(_secondDbContext, mappedData, ProjectId, WhichDatabase);
                }

                return Ok(new { message = "Registration data uploaded successfully" });
            }
            catch (DbUpdateException ex)
            {
                return StatusCode(500, new { message = $"An error occurred: {ex.InnerException?.Message ?? ex.Message}" });
            }
        }

        private async Task SaveToDatabase(DbContext dbContext, List<Dictionary<string, string>> mappedData, int projectId, string whichDatabase)
        {
            foreach (var row in mappedData)
            {
                if (!row.ContainsKey("Roll Number") || string.IsNullOrWhiteSpace(row["Roll Number"]))
                {
                    continue; // Skip this row
                }

                var registrationData = new RegistrationData
                {
                    RegistrationId = GetNextRegId(whichDatabase),
                    RegistrationsData = JsonConvert.SerializeObject(row.Where(kv => kv.Key != "Roll Number").ToDictionary(kv => kv.Key, kv => kv.Value)),
                    RollNumber = row.ContainsKey("Roll Number") ? row["Roll Number"] : null,
                    ProjectId = projectId
                };
                dbContext.Add(registrationData);

                await dbContext.SaveChangesAsync();
            }
            var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userID))
            {
                _logger.LogEvent($"Registration Data Uploaded for Project : {projectId}", "Registration Data", userID, whichDatabase);
            }

        }

        [HttpPost("ByFilters")]
        public async Task<ActionResult<List<RegistrationData>>> GetRegistrationData(string WhichDatabase, [FromBody] RegistrationFilter filter, int ProjectId)
        {
            var results = new List<RegistrationData>();

            // Choose the correct database context
            IQueryable<RegistrationData> query = WhichDatabase == "Local" ? _firstDbContext.RegistrationDatas : _secondDbContext.RegistrationDatas;
            query = query.Where(r => r.ProjectId == ProjectId);

            // Apply filters conditionally
            if (filter.Filters != null)
            {
                foreach (var filterEntry in filter.Filters)
                {
                    var key = filterEntry.Key;
                    var value = filterEntry.Value;

                    if (key.Equals("RollNumber", StringComparison.OrdinalIgnoreCase))
                    {
                        query = query.Where(r => r.RollNumber == value);
                    }
                    else
                    {
                        query = query.Where(r => EF.Functions.Like(r.RegistrationsData, $"%\"{key}\":%{value}%"));
                    }
                }
            }

            // Execute the query and fetch results
            results = await query.ToListAsync();

            // Deserialize and return the results
            List<RegistrationData> registrationDatas = results.Select(r =>
            {
                var registrationData = System.Text.Json.JsonSerializer.Deserialize<RegistrationData>(r.RegistrationsData);
                registrationData.RollNumber = r.RollNumber;
                registrationData.ProjectId = r.ProjectId;
                registrationData.RegistrationsData = r.RegistrationsData;
                registrationData.RegistrationId = r.RegistrationId;
                return registrationData;
            }).ToList();

            return Ok(registrationDatas);
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteRegistration(string WhichDatabase, int ProjectId)
        {
            if (ProjectId <= 0)
            {
                return NotFound("ProjectId not Found");
            }
            try
            {
                if (WhichDatabase == "Local")
                {
                    var deleteregistration = await _firstDbContext.RegistrationDatas
                        .Where(a => a.ProjectId == ProjectId).ToListAsync();

                    if (!deleteregistration.Any())
                    {
                        return BadRequest("No data found for the ProjectId");
                    }

                    _firstDbContext.RegistrationDatas.RemoveRange(deleteregistration);
                    await _firstDbContext.SaveChangesAsync();
                    var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                    if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userID))
                    {
                        _logger.LogEvent($"Registration Data Deleted for Project : {ProjectId}", "Registration Data", userID, WhichDatabase);
                    }
                }
                else
                {
                    if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                    {
                        return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                    }

                    var deleteregistration = await _secondDbContext.RegistrationDatas
                        .Where(a => a.ProjectId == ProjectId).ToListAsync();

                    if (!deleteregistration.Any())
                    {
                        return BadRequest("No data found for the ProjectId");
                    }

                    _secondDbContext.RegistrationDatas.RemoveRange(deleteregistration);
                    await _secondDbContext.SaveChangesAsync();
                    var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                    if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userID))
                    {
                        _logger.LogEvent($"Registration Data Deleted for Project : {ProjectId}", "Registration Data", userID, WhichDatabase);
                    }
                }
                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        [HttpGet("GetKeys")]
        public async Task<ActionResult<RegistrationKeysResponse>> GetKeys(string whichDatabase, int ProjectId)
        {
            if (whichDatabase == "Online" && !await _connectionChecker.IsOnlineDatabaseAvailableAsync())
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
            }

            // Choose the correct database context
            IQueryable<RegistrationData> query = whichDatabase == "Local" ? _firstDbContext.RegistrationDatas : _secondDbContext.RegistrationDatas;

            // Fetch the first registration data record
            var registrationData = await query.FirstOrDefaultAsync(u => u.ProjectId == ProjectId);

            if (registrationData == null)
            {
                return NotFound("No registration data found.");
            }

            // Parse the JSON to extract keys
            var jsonData = registrationData.RegistrationsData;
            var keys = new List<string>();
            using (JsonDocument doc = JsonDocument.Parse(jsonData))
            {
                foreach (JsonProperty element in doc.RootElement.EnumerateObject())
                {
                    keys.Add(element.Name);
                }
            }

            // Add the "Roll Number" column to the keys
            keys.Add(nameof(registrationData.RollNumber));

            var response = new RegistrationKeysResponse { Keys = keys };

            return Ok(response);
        }

        [HttpGet("CountByProjectId")]
        public async Task<ActionResult<int>> GetCountByProjectId(string whichDatabase, int ProjectId)
        {
            if (whichDatabase == "Online" && !await _connectionChecker.IsOnlineDatabaseAvailableAsync())
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
            }

            // Choose the correct database context
            IQueryable<RegistrationData> query = whichDatabase == "Local" ? _firstDbContext.RegistrationDatas : _secondDbContext.RegistrationDatas;

            // Fetch the count
            int count = await query.CountAsync(r => r.ProjectId == ProjectId);

            return Ok(count);
        }

        [HttpGet("GetUniqueValues")]
        public async Task<ActionResult<List<string>>> GetUniqueValues(string whichDatabase, string key, int ProjectId)
        {
            if (whichDatabase == "Online" && !await _connectionChecker.IsOnlineDatabaseAvailableAsync())
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
            }
            // Choose the correct database context
            IQueryable<RegistrationData> query = whichDatabase == "Local" ? _firstDbContext.RegistrationDatas : _secondDbContext.RegistrationDatas;

            // Fetch all registration data records
            var registrationDatas = await query.Where(u => u.ProjectId == ProjectId).ToListAsync();

            // Extract unique values for the specified key
            var uniqueValues = new HashSet<string>();
            foreach (var registrationData in registrationDatas)
            {
                var jsonData = registrationData.RegistrationsData;
                using (JsonDocument doc = JsonDocument.Parse(jsonData))
                {
                    if (doc.RootElement.TryGetProperty(key, out JsonElement valueElement))
                    {
                        uniqueValues.Add(valueElement.GetString());
                    }
                }
            }

            return Ok(uniqueValues.ToList());
        }

        private int GetNextRegId(string whichDatabase)
        {
            return whichDatabase == "Local" ? _firstDbContext.RegistrationDatas.Max(c => (int?)c.RegistrationId) + 1 ?? 1 : _secondDbContext.RegistrationDatas.Max(c => (int?)c.RegistrationId) + 1 ?? 1;
        }
    }

    public class RegistrationKeysResponse
    {
        public List<string> Keys { get; set; }
    }
}
