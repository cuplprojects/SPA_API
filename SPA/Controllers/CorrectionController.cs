using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using SPA.Data;
using SPA;
using SPA.Services;
using Newtonsoft.Json;
using Microsoft.CodeAnalysis;
using SPA.Models;
using System.Security.Claims;
using System.Drawing.Printing;
using Microsoft.AspNetCore.Authorization;
using System.Text;

namespace SPA.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CorrectionController : ControllerBase
    {
        private readonly FirstDbContext _FirstDbcontext;
        private readonly SecondDbContext _SecondDbcontext;
        private readonly IChangeLogger _changelogger;
        private readonly ISecurityService _securityService;
        private readonly DatabaseConnectionChecker _connectionChecker;
        private readonly ILoggerService _logger;

        public CorrectionController(FirstDbContext context, SecondDbContext secondDbContext, IChangeLogger changelogger, ISecurityService securityService, DatabaseConnectionChecker connectionChecker, ILoggerService logger)
        {
            _FirstDbcontext = context;
            _SecondDbcontext = secondDbContext;
            _changelogger = changelogger;
            _securityService = securityService;
            _connectionChecker = connectionChecker;
            _logger = logger;
        }

        [HttpGet("GetFlagsByCategory")]
        public async Task<ActionResult<IEnumerable<Flag>>> GetFlagsByCategory(string WhichDatabase, int ProjectID, string FieldName)
        {
            var flags = new List<Flag>();
            if (WhichDatabase == "Local")
            {
                if (FieldName == "all")
                {
                    flags = await _FirstDbcontext.Flags
                        .Where(f => !f.isCorrected && f.ProjectId == ProjectID)
                        .OrderBy(f => f.FlagId)
                        .Take(10).
                        ToListAsync();
                }
                else
                {
                    flags = await _FirstDbcontext.Flags
                                      .Where(f => !f.isCorrected && f.ProjectId == ProjectID && f.Field == FieldName)
                                      .OrderBy(f => f.FlagId) // Ensures correct ordering
                                      .Take(10)
                                      .ToListAsync();
                }

            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                if (FieldName == "all")
                {
                    flags = await _SecondDbcontext.Flags
                        .Where(f => !f.isCorrected && f.ProjectId == ProjectID)
                        .OrderBy(f => f.FlagId)
                        .Take(10).
                        ToListAsync();
                }
                else
                {
                    flags = await _SecondDbcontext.Flags
                                       .Where(f => !f.isCorrected && f.ProjectId == ProjectID && f.Field == FieldName)
                                       .OrderBy(f => f.FlagId) // Ensures correct ordering
                                       .Take(10)
                                       .ToListAsync();
                }

            }

            return flags;
        }

        [HttpGet("GetFlagsByCategoryChosingParameters")]
        public async Task<ActionResult<IEnumerable<object>>> GetFlagsByCategoryFromWheretoWhere(
      string WhichDatabase, int ProjectID, string FieldName, int rangeStart, int rangeEnd, int userId)
        {
            await CleanupExpiredAssignments(WhichDatabase);

            List<Flag> flags;
            List<FlagAssignment> existingAssignments;
            List<OMRImage> images;

            // Use the appropriate DbContext
            if (WhichDatabase == "Local")
            {
                flags = await FetchFlags(_FirstDbcontext, ProjectID, FieldName, rangeStart, rangeEnd, userId);
                existingAssignments = await _FirstDbcontext.FlagAssignments
                    .Where(a => a.ProjectId == ProjectID && a.FieldName == FieldName &&
                        ((a.StartFlagId >= rangeStart && a.StartFlagId <= rangeEnd) ||
                            (a.EndFlagId >= rangeStart && a.EndFlagId <= rangeEnd)))
                    .ToListAsync();
                images = await _FirstDbcontext.OMRImages
                    .Where(img => img.ProjectId == ProjectID)
                    .ToListAsync();
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }

                flags = await FetchFlags(_SecondDbcontext, ProjectID, FieldName, rangeStart, rangeEnd, userId);
                existingAssignments = await _SecondDbcontext.FlagAssignments
                    .Where(a => a.ProjectId == ProjectID && a.FieldName == FieldName &&
                        ((a.StartFlagId >= rangeStart && a.StartFlagId <= rangeEnd) ||
                            (a.EndFlagId >= rangeStart && a.EndFlagId <= rangeEnd)))
                    .ToListAsync();
                images = await _SecondDbcontext.OMRImages
                    .Where(img => img.ProjectId == ProjectID)
                    .ToListAsync();
            }

            // Check for existing assignments
            if (existingAssignments.Any())
            {
                return BadRequest("The specified range is already assigned to another user.");
            }

            // Map images to flags
            string hostUrl = $"{Request.Scheme}://{Request.Host}";
            var result = flags.Select(flag => {
                // Find the image where the extracted barcode matches the flag's barcode
                var image = images.FirstOrDefault(img => img.OMRImagesName == flag.BarCode);
                // Check if the image has been found and handle Base64 in FilePath
                string imagePath = null;
                if (image != null)
                {
                    // If FilePath is Base64 encoded, decode it to a valid file path
                    if (image.FilePath.Contains("base64,"))
                    {
                        try
                        {
                            // Extract the Base64 string part after 'base64,'
                            string base64Data = image.FilePath.Split("base64,")[1];

                            // Decode the Base64 data back into a string (e.g., file path or decoded data)
                            byte[] data = Convert.FromBase64String(base64Data);
                            imagePath = Encoding.UTF8.GetString(data); // You might need to adjust this if it's not a string path
                        }
                        catch (FormatException ex)
                        {
                            // Handle any Base64 decoding errors
                            imagePath = null;
                            Console.WriteLine("Error decoding Base64 FilePath: " + ex.Message);
                        }
                    }
                    else
                    {
                        // If no Base64, use the FilePath as is, with formatted slashes
                        imagePath = image.FilePath;
                        imagePath = imagePath.Replace("\\", "/");
                        if (!imagePath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
                        {
                            imagePath += ".jpg";
                        }
                    }
                }

                return new
                {
                    flag.FlagId,
                    flag.Remarks,
                    flag.FieldNameValue,
                    flag.Field,
                    flag.BarCode,
                    flag.ProjectId,
                    flag.isCorrected,
                    flag.UpdatedByUserId,
                    ImagePath = imagePath // Return the decoded or original image path
                };
            });


            return Ok(result);
        }

        private async Task<List<Flag>> FetchFlags(DbContext dbContext, int projectId, string fieldName, int rangeStart, int rangeEnd, int userId)
        {
            var query = dbContext.Set<Flag>().Where(f => !f.isCorrected && f.ProjectId == projectId);

            if (fieldName != "all")
            {
                query = query.Where(f => f.Field == fieldName);
            }

            // Adjust range start to 0-based index
            rangeStart = Math.Max(rangeStart - 1, 0);

            return await query
                .OrderBy(f => f.FlagId)
                .Skip(rangeStart)
                .Take(rangeEnd - rangeStart)
                .ToListAsync();
        }

        [HttpDelete("DeleteAssignmentbyUserId")]
        public async Task<IActionResult> DeleteFlagAssignment(string WhichDatabase, int userId)
        {
            var flags = new List<FlagAssignment>();
            if (WhichDatabase == "Local")
            {
                flags = await _FirstDbcontext.FlagAssignments.Where(fa => fa.UserId == userId).ToListAsync();
                if (flags == null)
                {
                    return NotFound();
                }
                foreach (var flag in flags)
                {
                    _FirstDbcontext.FlagAssignments.Remove(flag);
                    var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                    if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userID))
                    {
                        _logger.LogEvent($"Flag Assignment of UserID : {userId} Deleted", "Flag", userID, WhichDatabase);
                    }
                }
                _FirstDbcontext.SaveChanges();
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                flags = await _SecondDbcontext.FlagAssignments.Where(fa => fa.UserId == userId).ToListAsync();
                if (flags == null)
                {
                    return NotFound();
                }
                foreach (var flag in flags)
                {
                    _SecondDbcontext.FlagAssignments.Remove(flag);
                    var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                    if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userID))
                    {
                        _logger.LogEvent($"Flag Assignment of UserID : {userId} Deleted", "Flag", userID, WhichDatabase);
                    }
                }
                _SecondDbcontext.SaveChanges();
            }



            return NoContent();
        }


        [HttpGet("GetFlags")]
        public async Task<ActionResult<IEnumerable<Flag>>> GetFlags(string WhichDatabase, int ProjectID, int id)
        {
            var flags = new List<Flag>();
            if (WhichDatabase == "Local")
            {
                flags = await _FirstDbcontext.Flags
                                      .Where(f => !f.isCorrected && f.ProjectId == ProjectID && f.FlagId >= id)
                                      .OrderBy(f => f.FlagId) // Ensures correct ordering
                                      .Take(10)
                                      .ToListAsync();
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                flags = await _SecondDbcontext.Flags
                                      .Where(f => !f.isCorrected && f.ProjectId == ProjectID && f.FlagId >= id)
                                      .OrderBy(f => f.FlagId) // Ensures correct ordering
                                      .Take(10)
                                      .ToListAsync();
            }

            return flags;
        }

        // GET: api/Correction/GetOmrData/{barcode}
        [HttpGet("GetOmrData/{barcode}")]
        public async Task<ActionResult<OMRdata>> GetOmrData(string WhichDatabase, string barcode)
        {
            OMRdata omrData = new OMRdata();
            if (WhichDatabase == "Local")
            {
                omrData = await _FirstDbcontext.OMRdatas.FirstOrDefaultAsync(o => o.BarCode == barcode);

                if (omrData == null)
                {
                    return NotFound();
                }
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                omrData = await _SecondDbcontext.OMRdatas.FirstOrDefaultAsync(o => o.BarCode == barcode);

                if (omrData == null)
                {
                    return NotFound();
                }
            }

            return omrData;
        }


        [HttpPost("SubmitCorrection")]
        public async Task<ActionResult> SubmitCorrection(string WhichDatabase, Cyphertext cyphertext, int status, int ProjectId)
        {
            string decryptedJson = _securityService.Decrypt(cyphertext.cyphertextt);
            // Deserialize the decrypted JSON into the CorrectionInput object
            var input = JsonConvert.DeserializeObject<CorrectionInput>(decryptedJson);
            OMRdata omrData = new OMRdata();
            CorrectedOMRData existingCorrectedData = new CorrectedOMRData();
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (WhichDatabase == "Local")
            {
                omrData = await _FirstDbcontext.OMRdatas.FirstOrDefaultAsync(o => o.BarCode == input.BarCode && o.ProjectId == ProjectId);
                if (omrData == null)
                {
                    return NotFound("OMR data not found for the given barcode.");
                }
                existingCorrectedData = await _FirstDbcontext.CorrectedOMRDatas.FirstOrDefaultAsync(o => o.BarCode == input.BarCode && o.ProjectId == ProjectId);
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                omrData = await _SecondDbcontext.OMRdatas.FirstOrDefaultAsync(o => o.BarCode == input.BarCode && o.ProjectId == ProjectId);
                if (omrData == null)
                {
                    return NotFound("OMR data not found for the given barcode.");
                }
                existingCorrectedData = await _SecondDbcontext.CorrectedOMRDatas.FirstOrDefaultAsync(o => o.BarCode == input.BarCode && o.ProjectId == ProjectId);
            }

            // Find the OMR data by barcode


            // Check if there is an existing corrected data entry 

            Dictionary<string, JsonElement> omrDataDict;

            // Deserialize existing OMR data JSON or corrected OMR data JSON
            if (existingCorrectedData != null)
            {
                omrDataDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(existingCorrectedData.CorrectedOmrData);
            }
            else
            {
                omrDataDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(omrData.OmrData);
            }

            if (omrDataDict == null)
            {
                return BadRequest("Invalid OMR data format.");
            }

            // Update the specific field with the new value in the corrected data
            /*if (omrDataDict.ContainsKey(input.FieldName))
            {
                omrDataDict[input.FieldName] = JsonDocument.Parse($"\"{input.Value}\"").RootElement;
            }*/
           /* else
            {
                return BadRequest($"Field '{input.FieldName}' not found in OMR data.");
            }*/
            if (WhichDatabase == "Local")
            {
                if (existingCorrectedData != null)
                {
                    var originalValue = omrDataDict[input.FieldName].ToString();
                    omrDataDict[input.FieldName] = JsonDocument.Parse($"\"{input.Value}\"").RootElement;
                    existingCorrectedData.CorrectedOmrData = System.Text.Json.JsonSerializer.Serialize(omrDataDict).Replace("\\u0027", "'");
                    _FirstDbcontext.CorrectedOMRDatas.Update(existingCorrectedData);
                    omrData.Status = status;
                    var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                    if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                    {
                        _logger.LogEvent($"Data corrected for {omrData.BarCode}: Field:{input.FieldName} : Original Value {originalValue}, New Value {input.Value}", "Correction", userId, WhichDatabase);
                    }
                }
                else
                {
                    var originalValue = omrDataDict[input.FieldName].ToString();
                    omrDataDict[input.FieldName] = JsonDocument.Parse($"\"{input.Value}\"").RootElement;
                    var correctedOMRData = new CorrectedOMRData
                    {
                        ProjectId = omrData.ProjectId,
                        CorrectedId = 0,
                        CorrectedOmrData = System.Text.Json.JsonSerializer.Serialize(omrDataDict).Replace("\\u0027", "'"),
                        OmrData = omrData.OmrData,
                        BarCode = omrData.BarCode,
                    };
                    _FirstDbcontext.CorrectedOMRDatas.Add(correctedOMRData);
                    omrData.Status = status;
                    await _FirstDbcontext.SaveChangesAsync();
                    var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                    if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                    {
                        _logger.LogEvent($"Data corrected for {omrData.BarCode}: Field:{input.FieldName} : Original Value {originalValue}, New Value {input.Value}", "Correction", userId, WhichDatabase);
                    }

                }
                var flagsToUpdate = _FirstDbcontext.Flags
                .Where(f => f.BarCode == omrData.BarCode && f.Field == input.FieldName && f.ProjectId == ProjectId && !f.isCorrected)
                .ToList();


                foreach (var flag in flagsToUpdate)
                {
                    flag.isCorrected = true;
                    var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                    if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                    {
                        flag.UpdatedByUserId = userId;
                    }
                }

                await _FirstDbcontext.SaveChangesAsync();
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                if (existingCorrectedData != null)
                {
                    var originalValue = omrDataDict[input.FieldName].ToString();
                    omrDataDict[input.FieldName] = JsonDocument.Parse($"\"{input.Value}\"").RootElement;
                    existingCorrectedData.CorrectedOmrData = System.Text.Json.JsonSerializer.Serialize(omrDataDict).Replace("\\u0027", "'");
                    omrData.Status = status;
                    _SecondDbcontext.CorrectedOMRDatas.Update(existingCorrectedData);
                    var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                    if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                    {
                        _logger.LogEvent($"Data corrected for {omrData.BarCode}: Field:{input.FieldName} : Original Value {originalValue}, New Value {input.Value}", "Correction", userId, WhichDatabase);
                    }
                }
                else
                {
                    var originalValue = omrDataDict[input.FieldName].ToString();
                    omrDataDict[input.FieldName] = JsonDocument.Parse($"\"{input.Value}\"").RootElement;
                    var correctedOMRData = new CorrectedOMRData
                    {
                        ProjectId = omrData.ProjectId,
                        CorrectedId = 0,
                        CorrectedOmrData = System.Text.Json.JsonSerializer.Serialize(omrDataDict).Replace("\\u0027", "'"),
                        OmrData = omrData.OmrData,
                        BarCode = omrData.BarCode,
                    };
                    _SecondDbcontext.CorrectedOMRDatas.Add(correctedOMRData);
                    omrData.Status = status;
                    await _FirstDbcontext.SaveChangesAsync();
                    var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                    if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                    {
                        _logger.LogEvent($"Data corrected for {omrData.BarCode}: Field:{input.FieldName} : Original Value {originalValue}, New Value {input.Value}", "Correction", userId, WhichDatabase);
                    }
                }

                var flagsToUpdate = _SecondDbcontext.Flags
                .Where(f => f.BarCode == omrData.BarCode && f.Field == input.FieldName && f.ProjectId == ProjectId && !f.isCorrected)
                .ToList();
                foreach (var flag in flagsToUpdate)
                {
                    flag.isCorrected = true;
                    var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                    if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                    {
                        //_logger.LogEvent($"Deleted BookletPDFData in PaperID: {paperID}", "BookletPdfData", userId);
                        flag.UpdatedByUserId = userId;
                    }

                }

                await _SecondDbcontext.SaveChangesAsync();
            }
            return CreatedAtAction(nameof(GetCorrectedOmrData), new { id = existingCorrectedData?.CorrectedId ?? 0 }, existingCorrectedData);
        }

        // GET: api/Correction/GetCorrectedOmrData/{id}
        [HttpGet("GetCorrectedOmrData/{id}")]
        public async Task<ActionResult<CorrectedOMRData>> GetCorrectedOmrData(int id)
        {
            var correctedOmrData = await _FirstDbcontext.CorrectedOMRDatas.FindAsync(id);

            if (correctedOmrData == null)
            {
                return NotFound();
            }

            return correctedOmrData;
        }
        private async Task CleanupExpiredAssignments(string WhichDatabase)
        {
            var expiredAssignments = new List<FlagAssignment>();
            if (WhichDatabase == "Local")
            {
                expiredAssignments = await _FirstDbcontext.FlagAssignments.Where(a => a.ExpiresAt < DateTime.UtcNow.AddMinutes(330)).ToListAsync();

                if (expiredAssignments.Any())
                {
                    _FirstDbcontext.FlagAssignments.RemoveRange(expiredAssignments);
                    await _FirstDbcontext.SaveChangesAsync();
                }
            }
            else
            {
                if (await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    expiredAssignments = await _SecondDbcontext.FlagAssignments.Where(a => a.ExpiresAt < DateTime.UtcNow.AddMinutes(330)).ToListAsync();

                    if (expiredAssignments.Any())
                    {
                        _SecondDbcontext.FlagAssignments.RemoveRange(expiredAssignments);
                        await _SecondDbcontext.SaveChangesAsync();
                    }

                }
            }

        }
    }

    public class CorrectionInput
    {
        public string? BarCode { get; set; }
        public string FieldName { get; set; }
        public string Value { get; set; }
    }
}

