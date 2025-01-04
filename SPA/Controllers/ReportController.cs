/*using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SPA.Data;
using SPA.Models.NonDBModels;
using SPA.Models;
using SPA.Services;
using Newtonsoft.Json;
using System.Dynamic;

namespace SPA.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReportController : ControllerBase
    {
        private readonly FirstDbContext _firstDbContext;
        private readonly SecondDbContext _secondDbContext;
        private readonly IChangeLogger _changeLogger;
        private readonly ISecurityService _securityService;

        public ReportController(FirstDbContext firstDbContext, SecondDbContext secondDbContext, IChangeLogger changeLogger, ISecurityService securityService)
        {
            _firstDbContext = firstDbContext;
            _secondDbContext = secondDbContext;
            _changeLogger = changeLogger;
            _securityService = securityService;
        }

        [HttpPost("GetFilteredData")]
        public async Task<ActionResult<List<ExpandoObject>>> GetFilteredData(string WhichDatabase, int ProjectId)
        {
            var data = new List<ExpandoObject>();

            IQueryable<RegistrationData> registrationQuery = null;
            IQueryable<Score> scoreQuery = null;
            IQueryable<OMRdata> omrQuery = null;
            IQueryable<CorrectedOMRData> correctedQuery = null;

            // Choose the correct database context
            if (WhichDatabase == "Local")
            {
                registrationQuery = _firstDbContext.RegistrationDatas.Where(r => r.ProjectId == ProjectId);
                scoreQuery = _firstDbContext.Scores.Where(s => s.ProjectId == ProjectId);
                omrQuery = _firstDbContext.OMRdatas.Where(s => s.ProjectId == ProjectId && s.Status == 1);
                correctedQuery = _firstDbContext.CorrectedOMRDatas.Where(s => s.ProjectId == ProjectId);
            }
            else
            {
                registrationQuery = _secondDbContext.RegistrationDatas.Where(r => r.ProjectId == ProjectId);
                scoreQuery = _secondDbContext.Scores.Where(s => s.ProjectId == ProjectId);
                omrQuery = _secondDbContext.OMRdatas.Where(s => s.ProjectId == ProjectId && s.Status == 1);
                correctedQuery = _secondDbContext.CorrectedOMRDatas.Where(s => s.ProjectId == ProjectId);
            }

            // Fetch data
            var registrationDataList = await registrationQuery.ToListAsync();
            var scoreList = await scoreQuery.ToListAsync();
            var omrDataList = await omrQuery.ToListAsync();
            var correctedOMRDataList = await correctedQuery.ToListAsync();

            // Create dictionaries for quick lookup
            var scoreDict = scoreList.ToDictionary(s => s.RollNumber, s => s.TotalScore);

            var omrDataDict = omrDataList.ToDictionary(
                omr =>
                {
                    var omrJson = JsonConvert.DeserializeObject<Dictionary<string, string>>(omr.OmrData);
                    return omrJson.GetValueOrDefault("Roll Number");
                },
                omr => new { omr.BarCode, omr.OmrData }
            );

            var correctedOmrDataDict = correctedOMRDataList.ToDictionary(
                corrected =>
                {
                    var correctedJson = JsonConvert.DeserializeObject<Dictionary<string, string>>(corrected.CorrectedOmrData);
                    return correctedJson.GetValueOrDefault("Roll Number");
                },
                corrected => new { corrected.BarCode, corrected.CorrectedOmrData }
            );

            // Create combined data
            foreach (var registration in registrationDataList)
            {
                var registrationJson = JsonConvert.DeserializeObject<Dictionary<string, string>>(registration.RegistrationsData);

                dynamic combinedData = new ExpandoObject();
                var combinedDataDict = (IDictionary<string, object>)combinedData;

                combinedData.RollNumber = registration.RollNumber;
                combinedData.MarksObtained = scoreDict.TryGetValue(registration.RollNumber, out var score) ? score.ToString() : "absent";

                // Add fields from registration data dynamically
                foreach (var kvp in registrationJson)
                {
                    combinedDataDict[kvp.Key] = kvp.Value;
                }

                // Add OMRData if roll number matches
                if (omrDataDict.TryGetValue(registration.RollNumber, out var omrData))
                {
                    // Deserialize OMRData
                    var omrDataJson = JsonConvert.DeserializeObject<Dictionary<string, string>>(omrData.OmrData);
                    // Add to AdditionalFields
                    foreach (var kvp in omrDataJson)
                    {
                        combinedDataDict[kvp.Key] = kvp.Value;
                    }
                    combinedData.OMRDataBarCode = omrData.BarCode;
                }

                // Add CorrectedOMRData if roll number matches
                if (correctedOmrDataDict.TryGetValue(registration.RollNumber, out var correctedOmrData))
                {
                    // Deserialize CorrectedOMRData
                    var correctedOmrDataJson = JsonConvert.DeserializeObject<Dictionary<string, string>>(correctedOmrData.CorrectedOmrData);
                    // Add to AdditionalFields
                    foreach (var kvp in correctedOmrDataJson)
                    {
                        combinedDataDict[kvp.Key] = kvp.Value;
                    }
                    combinedData.OMRDataBarCode = correctedOmrData.BarCode;
                }

                data.Add(combinedData);
            }

            if (!data.Any())
            {
                return NotFound("No data found for the specified fields.");
            }

            return Ok(data);
        }
    }
}
*/

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SPA.Data;
using SPA.Models.NonDBModels;
using SPA.Models;
using SPA.Services;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;


namespace SPA.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ReportController : ControllerBase
    {
        private readonly FirstDbContext _firstDbContext;
        private readonly SecondDbContext _secondDbContext;
        private readonly IChangeLogger _changeLogger;
        private readonly ISecurityService _securityService;
        private readonly DatabaseConnectionChecker _connectionChecker;

        public ReportController(FirstDbContext firstDbContext, SecondDbContext secondDbContext, IChangeLogger changeLogger, ISecurityService securityService, DatabaseConnectionChecker databaseConnectionChecker)
        {
            _firstDbContext = firstDbContext;
            _secondDbContext = secondDbContext;
            _changeLogger = changeLogger;
            _securityService = securityService;
            _connectionChecker = databaseConnectionChecker;
        }

        [HttpGet]
        public async Task<IActionResult> GetReportConfig(string WhichDatabase, int UserId)
        {

            IQueryable<Report> query;

            if (WhichDatabase == "Local")
            {
                query = _firstDbContext.Reports.Where(a => a.UserId == UserId);
            }
            else
            {
                query = _secondDbContext.Reports.Where(a => a.UserId == UserId);
            }

            var myconfig = await query.ToListAsync();

            if (myconfig == null || !myconfig.Any())
            {
                return NotFound("No report data found for the given UserId.");
            }

            return Ok(myconfig);
        }


        [HttpPost]
        public async Task<IActionResult> PostReportConfig(string WhichDatabase, int UserId, [FromBody] Report report)
        {
            if (report == null)
            {
                return BadRequest("Report data is null.");
            }

            // Generate a new ReportId
            report.ReportId = GetNextReportId(WhichDatabase);
            report.UserId = UserId;  // Ensure the UserId is set

            // Serialize ReportData to JSON format
            report.ReportData = JsonConvert.SerializeObject(report.ReportData);

            try
            {
                if (WhichDatabase == "Local")
                {
                    _firstDbContext.Reports.Add(report);
                    await _firstDbContext.SaveChangesAsync();
                }
                else
                {
                    _secondDbContext.Reports.Add(report);
                    await _secondDbContext.SaveChangesAsync();
                }

                var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                                        
                }
                // Log the insertion

                // Return a 201 Created response with the location of the newly created report
                return CreatedAtAction(nameof(GetReportConfig), new { WhichDatabase, UserId }, report);
            }
            catch (DbUpdateException ex)
            {
                // Handle specific database update exceptions
                if (ReportExists(UserId, WhichDatabase))  // Assuming RoleExists method checks for existing records
                {
                    return Conflict("Report with the same UserId already exists.");
                }
                else
                {
                    // Log exception or handle unexpected errors
                    return StatusCode(500, "Internal server error.");
                }
            }
        }


        [HttpPut]
        public async Task<IActionResult> PutReportConfig(string WhichDatabase, int UserId, int ReportId, [FromBody] Report report)
        {
            if (report == null || ReportId != report.ReportId)
            {
                return BadRequest("Invalid report data or ReportId mismatch.");
            }

            try
            {
                // Serialize ReportData to JSON format
                report.ReportData = JsonConvert.SerializeObject(report.ReportData);

                // Find the existing report
                Report existingReport;
                if (WhichDatabase == "Local")
                {
                    existingReport = await _firstDbContext.Reports.FirstOrDefaultAsync(r => r.ReportId == ReportId && r.UserId == UserId);
                    if (existingReport == null) return NotFound("Report not found in the local database.");
                }
                else
                {
                    existingReport = await _secondDbContext.Reports.FirstOrDefaultAsync(r => r.ReportId == ReportId && r.UserId == UserId);
                    if (existingReport == null) return NotFound("Report not found in the secondary database.");
                }

                // Update the existing report
                existingReport.ReportData = report.ReportData;

                // Save changes to the database
                if (WhichDatabase == "Local")
                {
                    _firstDbContext.Reports.Update(existingReport);
                    await _firstDbContext.SaveChangesAsync();
                }
                else
                {
                    _secondDbContext.Reports.Update(existingReport);
                    await _secondDbContext.SaveChangesAsync();
                }

                var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {

                }
                // Log the update

                return NoContent(); // Return 204 No Content, indicating the update was successful
            }
            catch (DbUpdateException ex)
            {
                // Handle specific database update exceptions
                return StatusCode(500, "Internal server error.");
            }
        }

        [AllowAnonymous]
        [HttpPost("GetFilteredData")]
        public async Task<ActionResult<List<CombinedData>>> GetFilteredData(string WhichDatabase, int ProjectId, [FromBody] DataFilter filter)
        {
            var data = new List<CombinedData>();

            IQueryable<RegistrationData> registrationQuery;
            IQueryable<Score> scoreQuery;
            IQueryable<OMRdata> omrQuery;
            IQueryable<CorrectedOMRData> correctedQuery;

            try
            {
                if (WhichDatabase == "Local")
                {
                    registrationQuery = _firstDbContext.RegistrationDatas.Where(r => r.ProjectId == ProjectId);
                    scoreQuery = _firstDbContext.Scores.Where(s => s.ProjectId == ProjectId);
                    omrQuery = _firstDbContext.OMRdatas.Where(s => s.ProjectId == ProjectId && s.Status == 1);
                    correctedQuery = _firstDbContext.CorrectedOMRDatas.Where(s => s.ProjectId == ProjectId);
                }
                else
                {
                    registrationQuery = _secondDbContext.RegistrationDatas.Where(r => r.ProjectId == ProjectId);
                    scoreQuery = _secondDbContext.Scores.Where(s => s.ProjectId == ProjectId);
                    omrQuery = _secondDbContext.OMRdatas.Where(s => s.ProjectId == ProjectId && s.Status == 1);
                    correctedQuery = _secondDbContext.CorrectedOMRDatas.Where(s => s.ProjectId == ProjectId);
                }
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }

            var registrationDataList = await registrationQuery.ToListAsync();
            var scoreList = await scoreQuery.ToListAsync();
            var omrDataList = await omrQuery.ToListAsync();
            var correctedOmrDataList = await correctedQuery.ToListAsync();

            var scoreDict = scoreList.ToDictionary(s => s.RollNumber, s => s.TotalScore);

            var omrDataDict = omrDataList.ToDictionary(
                omr =>
                {
                    var omrJson = JsonConvert.DeserializeObject<Dictionary<string, string>>(omr.OmrData);
                    return omrJson.GetValueOrDefault("Roll Number");
                },
                omr => new { omr.BarCode, omr.OmrData }
            );

            var correctedOmrDataDict = correctedOmrDataList.ToDictionary(
                corrected =>
                {
                    var correctedJson = JsonConvert.DeserializeObject<Dictionary<string, string>>(corrected.CorrectedOmrData);
                    return correctedJson.GetValueOrDefault("Roll Number");
                },
                corrected => new { corrected.BarCode, corrected.CorrectedOmrData }
            );

            if (registrationDataList.Any())
            {
                // Original algorithm: loop through registrationDataList
                foreach (var registration in registrationDataList)
                {
                    var registrationJson = JsonConvert.DeserializeObject<Dictionary<string, string>>(registration.RegistrationsData);

                    var combinedData = new CombinedData
                    {
                        RollNumber = registration.RollNumber,
                        MarksObtained = scoreDict.TryGetValue(registration.RollNumber, out var score) ? score.ToString() : "absent"
                    };

                    // Add fields from registration data dynamically
                    foreach (var kvp in registrationJson)
                    {
                        combinedData.RegistrationData[kvp.Key] = kvp.Value;
                    }

                    // Add OMRData if roll number matches
                    if (omrDataDict.TryGetValue(registration.RollNumber, out var omrData))
                    {
                        combinedData.OMRData = omrData.OmrData;
                        combinedData.OMRDataBarCode = omrData.BarCode;
                    }

                    // Add CorrectedOMRData if roll number matches
                    if (correctedOmrDataDict.TryGetValue(registration.RollNumber, out var correctedOmrData))
                    {
                        combinedData.OMRData = correctedOmrData.CorrectedOmrData;
                        combinedData.OMRDataBarCode = correctedOmrData.BarCode;
                    }

                    data.Add(combinedData);
                }
            }
            else
            {
                // Modified algorithm: gather all unique roll numbers from all sources
                var allRollNumbers = scoreDict.Keys
                    .Union(omrDataDict.Keys)
                    .Union(correctedOmrDataDict.Keys)
                    .Distinct();

                foreach (var rollNumber in allRollNumbers)
                {
                    var combinedData = new CombinedData
                    {
                        RollNumber = rollNumber,
                        MarksObtained = scoreDict.TryGetValue(rollNumber, out var score) ? score.ToString() : "absent"
                    };

                    // Add fields from registration data if available
                    if (registrationDataList.FirstOrDefault(r => r.RollNumber == rollNumber) is RegistrationData registration)
                    {
                        var registrationJson = JsonConvert.DeserializeObject<Dictionary<string, string>>(registration.RegistrationsData);
                        foreach (var kvp in registrationJson)
                        {
                            combinedData.RegistrationData[kvp.Key] = kvp.Value;
                        }
                    }

                    // Add OMRData if roll number matches
                    if (omrDataDict.TryGetValue(rollNumber, out var omrData))
                    {
                        combinedData.OMRData = omrData.OmrData;
                        combinedData.OMRDataBarCode = omrData.BarCode;
                    }

                    // Add CorrectedOMRData if roll number matches
                    if (correctedOmrDataDict.TryGetValue(rollNumber, out var correctedOmrData))
                    {
                        combinedData.OMRData = correctedOmrData.CorrectedOmrData;
                        combinedData.OMRDataBarCode = correctedOmrData.BarCode;
                    }

                    data.Add(combinedData);
                }
            }

            if (!data.Any())
            {
                return NotFound("No data found for the specified fields.");
            }

            return Ok(data);
        }


        private bool ReportExists(int id, string WhichDatabase)
        {
            if (WhichDatabase == "Local")
            {
                return _firstDbContext.Reports.Any(e => e.ReportId == id);
            }
            else
            {
                if (!_connectionChecker.IsOnlineDatabaseAvailable())
                {
                    return false;
                }
                return _secondDbContext.Reports.Any(e => e.ReportId == id);
            }
        }

        private int GetNextReportId(string WhichDatabase)
        {
            return WhichDatabase == "Local" ? _firstDbContext.Reports.Max(c => (int?)c.ReportId) + 1 ?? 1 : _secondDbContext.Reports.Max(c => (int?)c.ReportId) + 1 ?? 1;
        }






    }
    public class DataFilter
    {
        public List<string> Fields { get; set; } // List of fields to include in the response
    }

    public class CombinedData
    {
        public string RollNumber { get; set; }
        public string MarksObtained { get; set; }
        public string OMRData { get; set; }
        public string OMRDataBarCode { get; set; }
        public Dictionary<string, string> RegistrationData { get; set; } = new Dictionary<string, string>();
    }

}
