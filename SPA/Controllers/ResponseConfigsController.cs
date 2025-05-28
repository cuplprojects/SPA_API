using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SPA;
using SPA.Models.NonDBModels;
using SPA.Data;
using Newtonsoft.Json;
using SPA.Services;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;
using Newtonsoft.Json.Linq;
using System.Drawing.Printing;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.CodeAnalysis;
using DocumentFormat.OpenXml.Office2010.Excel;
using DocumentFormat.OpenXml.Spreadsheet;

namespace SPA.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ResponseConfigsController : ControllerBase
    {
        private readonly FirstDbContext _firstDbContext;
        private readonly SecondDbContext _secondDbContext;
        private readonly IChangeLogger _changeLogger;
        private readonly ISecurityService _securityService;
        private readonly DatabaseConnectionChecker _connectionChecker;
        private readonly ILoggerService _logger;



        public ResponseConfigsController(FirstDbContext firstDbcontext, SecondDbContext secondDbContext, IChangeLogger changeLogger, ISecurityService securityService, DatabaseConnectionChecker connectionChecker, ILoggerService logger)
        {
            _firstDbContext = firstDbcontext;
            _secondDbContext = secondDbContext;
            _changeLogger = changeLogger;
            _securityService = securityService;
            _connectionChecker = connectionChecker;
            _logger = logger;
        }

        // GET: api/ResponseConfigs
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ResponseConfig>>> GetResponseConfigs(string WhichDatabase)
        {
            if (WhichDatabase == "Local")
            {
                return await _firstDbContext.ResponseConfigs.ToListAsync();
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                return await _secondDbContext.ResponseConfigs.ToListAsync();
            }

        }

        [HttpGet("byproject/{projectId}")]
        public async Task<ActionResult<IEnumerable<ResponseConfig>>> GetResponseConfigsByProject(int projectId, string WhichDatabase)
        {
            try
            {
                List<ResponseConfig> responseConfigs = new List<ResponseConfig>();

                if (WhichDatabase == "Local")
                {
                    responseConfigs = await _firstDbContext.ResponseConfigs
                        .Where(rc => rc.ProjectId == projectId)
                        .ToListAsync();
                }
                else
                {
                    if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                    {
                        return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                    }
                    responseConfigs = await _secondDbContext.ResponseConfigs
                        .Where(rc => rc.ProjectId == projectId)
                        .ToListAsync();
                }

                return responseConfigs;
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, $"Error retrieving response configs by project ID: {ex.Message}");
            }
        }


        // GET: api/ResponseConfigs/5
        [HttpGet("{id}")]
        public async Task<ActionResult<ResponseConfig>> GetResponseConfig(int id, string WhichDatabase)
        {

            ResponseConfig responseConfig = new ResponseConfig();
            if (WhichDatabase == "Local")
            {
                responseConfig = await _firstDbContext.ResponseConfigs.FindAsync(id);
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                responseConfig = await _secondDbContext.ResponseConfigs.FindAsync(id);
            }

            if (responseConfig == null)
            {
                return NotFound();
            }

            return responseConfig;
        }

        // PUT: api/ResponseConfigs/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutResponseConfig(int id, Cyphertext cyphertext, string WhichDatabase)
        {
            // Decrypt the cyphertext
            string decryptedJson = _securityService.Decrypt(cyphertext.cyphertextt);

            // Deserialize the decrypted JSON into a temporary object
            var tempResponseConfig = JsonConvert.DeserializeObject<JObject>(decryptedJson);

            // Verify the sections structure
            var sectionsToken = tempResponseConfig["sections"];
            var sections = sectionsToken?.ToObject<List<Section>>();

            var responseConfig = new ResponseConfig
            {
                ResponseId = id,
                ResponseOption = tempResponseConfig["responseOption"]?.ToString(),
                NumberOfBlocks = (int)tempResponseConfig["numberOfBlocks"],
                ProjectId = tempResponseConfig["projectId"]?.ToObject<int>(),
                Sections = sections ?? new List<Section>()
            };

            // Verify that SectionsJson is correctly set
            responseConfig.SectionsJson = JsonConvert.SerializeObject(responseConfig.Sections);

            // Debug: Log the SectionsJson after setting it
            Console.WriteLine("SectionsJson: " + responseConfig.SectionsJson);

            if (WhichDatabase == "Local")
            {
                _firstDbContext.Entry(responseConfig).State = EntityState.Modified;
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                _secondDbContext.Entry(responseConfig).State = EntityState.Modified;
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
            catch (DbUpdateConcurrencyException)
            {
                if (!ResponseConfigExists(id, WhichDatabase))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userID))
            {
                _logger.LogEvent($"Response Config : {id} Updated", "Response Configuration", userID, WhichDatabase);
            }
            return NoContent();
        }

        [AllowAnonymous]
        [HttpGet("SectionName")]
        public async Task<ActionResult> GetSectionNames(int projectId, string courseName, string WhichDatabase)
        {
            List<string> sectionNames = new();
            List<string> finalSectionNames = new();
            List<FieldConfig> fields;
            List<string> bookletlist;

            if (WhichDatabase == "Local")
            {
                sectionNames = await _firstDbContext.ResponseConfigs
                    .Where(rc => rc.ProjectId == projectId && rc.CourseName == courseName)
                    .SelectMany(rc => JsonConvert.DeserializeObject<List<Section>>(rc.SectionsJson))
                    .Select(s => s.Name)
                    .ToListAsync();

                fields = await _firstDbContext.FieldConfigs
                    .Where(fc => fc.ProjectId == projectId)
                    .ToListAsync();

                bookletlist = await _firstDbContext.FieldConfigs
                    .Where(bc => bc.ProjectId == projectId && bc.FieldName == "Booklet Series")
                    .Select(bc => bc.FieldAttributesJson)
                    .ToListAsync();
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }

                sectionNames = await _secondDbContext.ResponseConfigs
                    .Where(rc => rc.ProjectId == projectId && rc.CourseName == courseName)
                    .SelectMany(rc => JsonConvert.DeserializeObject<List<Section>>(rc.SectionsJson))
                    .Select(s => s.Name)
                    .ToListAsync();

                fields = await _secondDbContext.FieldConfigs
                    .Where(fc => fc.ProjectId == projectId)
                    .ToListAsync();

                bookletlist = await _secondDbContext.FieldConfigs
                    .Where(bc => bc.ProjectId == projectId && bc.FieldName == "Booklet Series")
                    .Select(bc => bc.FieldAttributesJson)
                    .ToListAsync();
            }

            var distinctSectionNames = sectionNames.Distinct().ToList();

            foreach (var section in distinctSectionNames)
            {
                var matchedField = fields.FirstOrDefault(f => f.FieldName == section);
                if (matchedField != null && !string.IsNullOrEmpty(matchedField.FieldAttributesJson))
                {
                    try
                    {
                        var attributes = JsonConvert.DeserializeObject<List<FieldAttribute>>(matchedField.FieldAttributesJson);
                        foreach (var attr in attributes)
                        {
                            var responses = attr.Responses?.Split(',') ?? Array.Empty<string>();
                            foreach (var resp in responses)
                            {
                                finalSectionNames.Add($"{section}:{resp.Trim()}");
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        Console.WriteLine($"Error parsing FieldAttributesJson for section {section}: {ex.Message}");
                    }
                }
                else
                {
                    // If no matching FieldConfig, just add section name
                    finalSectionNames.Add(section);
                }
            }

            // Extract fieldnames from Booklet Series only
            List<string> fieldnames = new();

            foreach (var json in bookletlist)
            {
                if (!string.IsNullOrEmpty(json))
                {
                    try
                    {
                        var attributes = JsonConvert.DeserializeObject<List<FieldAttribute>>(json);
                        foreach (var attr in attributes)
                        {
                            var responses = attr.Responses?.Split(',') ?? Array.Empty<string>();
                            foreach (var resp in responses)
                            {
                                fieldnames.Add(resp.Trim());
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        Console.WriteLine($"Error parsing Booklet Series FieldAttributesJson: {ex.Message}");
                    }
                }
            }

            if (!fieldnames.Any())
            {
                fieldnames.Add("A");
            }

            return Ok(new
            {
                sectionNames = finalSectionNames,
                fieldnames = fieldnames.Distinct()
            });
        }




        // POST: api/ResponseConfigs
        [HttpPost]
        public async Task<ActionResult<ResponseConfig>> PostResponseConfig(Cyphertext cyphertext, string WhichDatabase)
        {
            int newResponseConfigID = GetNextResponseConfigId(WhichDatabase);

            string decryptedJson = _securityService.Decrypt(cyphertext.cyphertextt);

            var tempResponseConfig = JsonConvert.DeserializeObject<JObject>(decryptedJson);

            var sectionsToken = tempResponseConfig["sections"];
            var sections = sectionsToken?.ToObject<List<Section>>();
            var ProjectID = tempResponseConfig["projectId"]?.ToObject<int>();
            var CourseName = tempResponseConfig["courseName"]?.ToString();
            bool configExists = false;
            if (WhichDatabase == "Local")
            {
                configExists = _firstDbContext.ResponseConfigs
                    .Any(rc => rc.ProjectId == ProjectID && rc.CourseName == CourseName);
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                configExists = _secondDbContext.ResponseConfigs
                    .Any(rc => rc.ProjectId == ProjectID && rc.CourseName == CourseName);
            }

            if (configExists)
            {
                return Conflict($"A response configuration for Project ID '{ProjectID}' and Course Name '{CourseName}' already exists.");
            }
            var responseConfig = new ResponseConfig
            {
                ResponseId = newResponseConfigID,
                ResponseOption = tempResponseConfig["responseOption"]?.ToString(),
                NumberOfBlocks = (int)tempResponseConfig["numberOfBlocks"],
                ProjectId = tempResponseConfig["projectId"]?.ToObject<int>(),
                CourseName = tempResponseConfig["courseName"]?.ToString(),
                Sections = sections ?? new List<Section>()
            };

            // Verify that SectionsJson is correctly set
            responseConfig.SectionsJson = JsonConvert.SerializeObject(responseConfig.Sections);

            // Debug: Log the SectionsJson after setting it
            Console.WriteLine("SectionsJson: " + responseConfig.SectionsJson);

            // Get the next available ResponseConfigId
            int newResponseConfigId = GetNextResponseConfigId(WhichDatabase);
            responseConfig.ResponseId = newResponseConfigId;

            if (WhichDatabase == "Local")
            {
                _firstDbContext.ResponseConfigs.Add(responseConfig);
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                _secondDbContext.ResponseConfigs.Add(responseConfig);
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
                if (ResponseConfigExists(responseConfig.ResponseId, WhichDatabase))
                {
                    return Conflict();
                }
                else
                {
                    throw;
                }
            }

            string responseJson = JsonConvert.SerializeObject(responseConfig);
            string jsonUpdate = responseJson.Replace("\\\"", "'").Trim();

            var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userID))
            {
                _logger.LogEvent($"Response Config for Project: {ProjectID}, Course : {CourseName} Added", "Response Configuration", userID, WhichDatabase);
            }
            return CreatedAtAction("GetResponseConfig", new { id = responseConfig.ResponseId }, responseConfig);
        }

        // DELETE: api/ResponseConfigs/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteResponseConfig(int id, string WhichDatabase)
        {
            if (WhichDatabase == "Local")
            {
                var responseconfig = await _firstDbContext.ResponseConfigs.FindAsync(id);
                if (responseconfig == null)
                {
                    return NotFound();
                }
                _firstDbContext.ResponseConfigs.Remove(responseconfig);
                var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    _logger.LogEvent($"Response Config for Project: {responseconfig.ProjectId}, Course : {responseconfig.CourseName} Deleted", "Response Configuration", userId, WhichDatabase);
                }
                await _firstDbContext.SaveChangesAsync();
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                var responseconfig = await _secondDbContext.ResponseConfigs.FindAsync(id);
                if (responseconfig == null)
                {
                    return NotFound();
                }
                _secondDbContext.ResponseConfigs.Remove(responseconfig);
                var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    _logger.LogEvent($"Response Config for Project: {responseconfig.ProjectId}, Course : {responseconfig.CourseName} Deleted", "Response Configuration", userId, WhichDatabase);
                }
                await _secondDbContext.SaveChangesAsync();
            }

            return NoContent();
        }

        [HttpGet("unique")]
        public async Task<IActionResult> GetCoursename(int ProjectId, string WhichDatabase)
        {
            IEnumerable<string> uniqueCourseNames;

            if (WhichDatabase == "Local")
            {
                uniqueCourseNames = await _firstDbContext.ResponseConfigs.Where(r => r.ProjectId == ProjectId).Select(r => r.CourseName).Distinct().ToListAsync();

            }
            else
            {
                uniqueCourseNames = await _secondDbContext.ResponseConfigs.Where(r => r.ProjectId == ProjectId).Select(r => r.CourseName).Distinct().ToListAsync();
            }
            return Ok(uniqueCourseNames);
        }

        private bool ResponseConfigExists(int id, string WhichDatabase)
        {
            if (WhichDatabase == "Local")
            {
                return _firstDbContext.ResponseConfigs.Any(e => e.ResponseId == id);
            }
            else
            {
                if (_connectionChecker.IsOnlineDatabaseAvailable())
                {
                    return _secondDbContext.ResponseConfigs.Any(e => e.ResponseId == id);
                }
                return false;
            }
        }

        private int GetNextResponseConfigId(string WhichDatabase)
        {
            return WhichDatabase == "Local" ? _firstDbContext.ResponseConfigs.Max(c => (int?)c.ResponseId) + 1 ?? 1 : _secondDbContext.ResponseConfigs.Max(c => (int?)c.ResponseId) + 1 ?? 1;
        }
    }

    public class Cyphertext
    {
        public string cyphertextt { get; set; }
    }
}
