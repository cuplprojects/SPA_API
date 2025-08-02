
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SPA.Data;
using SPA.Models.NonDBModels;
using SPA.Models;
using SPA.Services;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Text.Json;


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


        public class FlagDto
        {
            public int Id { get; set; }
            public string Remarks { get; set; }
            public string BarCode { get; set; }
            public string Field { get; set; }
            public string FieldNameValue { get; set; }
            public string CorrectedValue { get; set; } // Extracted value from JSON
            public bool isCorrected { get; set; }
            public int ProjectId { get; set; }
            public int ? UpdatedByUserId { get; set; } // Assuming you want to include UserId as well
        }

        [HttpGet("ByProject/{ProjectId}")]
        public async Task<ActionResult<List<FlagDto>>> GetFlagbyProjectForReport(int ProjectId, string WhichDatabase)
        {
           
                var flags = await _firstDbContext.Flags
                                    .Where(o => o.ProjectId == ProjectId)
                                    .ToListAsync();

                var result = new List<FlagDto>();

                foreach (var flag in flags)
                {
                    var dto = new FlagDto
                    {
                        Id = flag.FlagId,
                        Remarks = flag.Remarks,
                        BarCode = flag.BarCode,
                        FieldNameValue = flag.FieldNameValue,
                        Field = flag.Field,
                        isCorrected = flag.isCorrected,
                        ProjectId = flag.ProjectId,
                        UpdatedByUserId = flag.UpdatedByUserId,
                    };

                    if (flag.isCorrected)
                    {
                        var correctedOmr = await _firstDbContext.CorrectedOMRDatas
                            .FirstOrDefaultAsync(o => o.ProjectId == ProjectId && o.BarCode == flag.BarCode);

                        if (correctedOmr != null && !string.IsNullOrWhiteSpace(correctedOmr.CorrectedOmrData))
                        {
                            try
                            {
                                using var doc = JsonDocument.Parse(correctedOmr.CorrectedOmrData);
                                var root = doc.RootElement;

                                if (root.TryGetProperty(flag.Field, out var fieldValue))
                                {
                                    dto.CorrectedValue = fieldValue.GetString();
                                }
                                else
                                {
                                    dto.CorrectedValue = $"Field '{flag.Field}' not found in JSON";
                                }
                            }
                            catch (Newtonsoft.Json.JsonException)
                            {
                                dto.CorrectedValue = "Invalid JSON format";
                            }
                        }
                        else
                        {
                            dto.CorrectedValue = "Corrected OMR not found";
                        }
                    }

                    result.Add(dto);
                }

                if (!result.Any())
                    return NotFound();

                return result;
            
          
        }
        [AllowAnonymous]
        [HttpGet("ComparisonReport/{ProjectId}")]
        public async Task<ActionResult<List<object>>> GetComparisonReport(int ProjectId, string whichDatabase)
        {
            var flags = await _firstDbContext.Flags
                .Where(p => p.ProjectId == ProjectId &&
                            (EF.Functions.Like(p.Remarks, "%Mismatch in Question:%") ||
                             EF.Functions.Like(p.Remarks, "%Missing in Extracted%")))
                .ToListAsync();

            var groupedData = flags
                .GroupBy(x => x.BarCode)
                .Select(g => new
                {
                    Barcode = g.Key,
                    FieldCounts = g.GroupBy(f => f.Field)
                           .ToDictionary(fg => fg.Key, fg => fg.Count())
                })
                .ToList();

            return Ok(groupedData);
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
