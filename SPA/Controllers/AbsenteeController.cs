using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SPA.Data;
using SPA.Services;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SPA.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AbsenteeController : ControllerBase
    {
        private readonly FirstDbContext _firstDbContext;
        private readonly SecondDbContext _secondDbContext;
        private readonly IChangeLogger _changeLogger;
        private readonly ISecurityService _securityService;
        private readonly DatabaseConnectionChecker _connectionChecker;
        private readonly ILoggerService _logger;

        public AbsenteeController(FirstDbContext firstDbcontext, SecondDbContext secondDbContext, IChangeLogger changeLogger, ISecurityService securityService, DatabaseConnectionChecker connectionChecker, ILoggerService logger)
        {
            _firstDbContext = firstDbcontext;
            _secondDbContext = secondDbContext;
            _changeLogger = changeLogger;
            _securityService = securityService;
            _connectionChecker = connectionChecker;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<string>> GetAbsenteeList(string WhichDatabase, int ProjectId)
        {
            if (WhichDatabase == "Local")
            {
                var absentees = _firstDbContext.Absentees.Where(a => a.ProjectID == ProjectId).Select(u => u.RollNo).ToList();
                var encrypteddata = _securityService.Encrypt(JsonConvert.SerializeObject(absentees));

                return encrypteddata;
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                var absentees = _secondDbContext.Absentees.Where(a => a.ProjectID == ProjectId).Select(u => u.RollNo).ToList();
                var encrypteddata = _securityService.Encrypt(JsonConvert.SerializeObject(absentees));

                return encrypteddata;
            }
        }


        [HttpGet("absentee/count/{projectId}")]
        public async Task<ActionResult<int>> GetTotalAbsenteesCount(int projectId, string WhichDatabase)
        {
            try
            {
                int totalCount = 0;

                if (WhichDatabase == "Local")
                {
                    // Query total count from the first database context
                    totalCount = await _firstDbContext.Absentees
                        .Where(a => a.ProjectID == projectId)
                        .CountAsync();
                }
                else
                {
                    if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                    {
                        return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                    }
                    // Query total count from the second database context
                    totalCount = await _secondDbContext.Absentees
                        .Where(a => a.ProjectID == projectId)
                        .CountAsync();
                }

                return Ok(totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString(), ex.Message, "Absentees", WhichDatabase);
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }


        [HttpGet("absentee/mapping-fields")]
        public async Task<ActionResult<IEnumerable<string>>> GetAbsenteeMappingFields(string WhichDatabase)
        {
            if (WhichDatabase == "Local")
            {
                var absenteeProperties = typeof(Absentee).GetProperties()
                    .Select(p => p.Name)
                    .Where(name => name != "AbsenteeId" && name != "ProjectID")
                    .ToList();

                return Ok(absenteeProperties);
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }

                var absenteeProperties = typeof(Absentee).GetProperties()
                       .Select(p => p.Name)
                       .Where(name => name != "AbsenteeId" && name != "ProjectID")
                       .ToList();

                return Ok(absenteeProperties);
            }
        }

     

        [HttpPost("upload")]
        [RequestSizeLimit(104857600)]
        public async Task<IActionResult> UploadAbsentees(Cyphertext cyphertext, string WhichDatabase)
        {
            if (cyphertext == null || string.IsNullOrEmpty(cyphertext.cyphertextt))
            {
                return BadRequest("Cyphertext is null or empty.");
            }

            if (string.IsNullOrEmpty(WhichDatabase))
            {
                return BadRequest("Database selection is missing.");
            }

            // Decrypt the cyphertext
            string decryptedJson = _securityService.Decrypt(cyphertext.cyphertextt);

            // Deserialize the decrypted JSON into the List<Absentee> object
            List<Absentee> absentees = JsonConvert.DeserializeObject<List<Absentee>>(decryptedJson);

            // Validate the list of absentees
            if (absentees == null || absentees.Count == 0)
            {
                return BadRequest("No data provided.");
            }

            try
            {
                if (WhichDatabase == "Local")
                {
                    foreach (var absentee in absentees)
                    {
                        absentee.AbsenteeId = GetNextAbsenteeId(WhichDatabase);
                        _firstDbContext.Absentees.Add(absentee);
                        await _firstDbContext.SaveChangesAsync(); // Ensure changes are saved immediately
                        await LogAbsenteeEntry(absentee, WhichDatabase);
                    }
                }
                else
                {
                    if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                    {
                        return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                    }
                    foreach (var absentee in absentees)
                    {
                        absentee.AbsenteeId = GetNextAbsenteeId(WhichDatabase);
                        _secondDbContext.Absentees.Add(absentee);
                        await _secondDbContext.SaveChangesAsync(); // Ensure changes are saved immediately
                        await LogAbsenteeEntry(absentee, WhichDatabase);
                    }
                }
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex.ToString(), ex.Message, "Absentees", WhichDatabase);
                return BadRequest(ex.Message);
            }

            return Ok(new { message = "Absentees uploaded successfully." });
        }

        private async Task LogAbsenteeEntry(Absentee absentee, string WhichDatabase)
        {
            var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                try
                {
                    _logger.LogEvent($"Absentee Entry Added for {absentee.RollNo}", "Absentee", userId, WhichDatabase);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }

        private int GetNextAbsenteeId(string WhichDatabase)
        {
            return WhichDatabase == "Local" ? _firstDbContext.Absentees.Max(c => (int?)c.AbsenteeId) + 1 ?? 1 : _secondDbContext.Absentees.Max(c => (int?)c.AbsenteeId) + 1 ?? 1;
        }

     
        [HttpDelete]
        public async Task<IActionResult> DeleteAbsentee(int ProjectId, string WhichDatabase)
        {
            if (ProjectId <= 0)
            {
                return NotFound("ProjectId not Found");
            }

            try
            {
                if (WhichDatabase == "Local")
                {
                    if (_firstDbContext == null)
                    {
                        return StatusCode(500, "Local database context is not initialized.");
                    }

                    var deleteabsentee = await _firstDbContext.Absentees
                        .Where(p => p.ProjectID == ProjectId).ToListAsync();

                    if (deleteabsentee == null || deleteabsentee.Count == 0)
                    {
                        return Ok("No absentee records found for this ProjectId in the Local database.");
                    }

                    _firstDbContext.Absentees.RemoveRange(deleteabsentee);
                    await _firstDbContext.SaveChangesAsync();
                }
                else
                {
                    if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                    {
                        return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                    }

                    if (_secondDbContext == null)
                    {
                        return StatusCode(500, "Online database context is not initialized.");
                    }

                    var deleteabsentee = await _secondDbContext.Absentees
                        .Where(p => p.ProjectID == ProjectId).ToListAsync();

                    if (deleteabsentee == null || deleteabsentee.Count == 0)
                    {
                        return Ok("No absentee records found for this ProjectId in the Online database.");
                    }

                    _secondDbContext.Absentees.RemoveRange(deleteabsentee);
                    await _secondDbContext.SaveChangesAsync();
                }

                var userIdClaim = HttpContext?.User?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userID))
                {
                    _logger.LogEvent($"Absentee Data Deleted for Project : {ProjectId}", "Registration Data", userID, WhichDatabase);
                }

                return Ok("Absentees deleted successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString(), ex.Message, "Absentees", WhichDatabase);
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

    }
}
