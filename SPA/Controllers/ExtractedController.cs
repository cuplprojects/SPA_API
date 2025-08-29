using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SPA.Data;
using SPA.Models;
using SPA.Services;
using System.Security.Claims;

namespace SPA.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ExtractedController : ControllerBase
    {
        private readonly FirstDbContext _context;
            private readonly ILoggerService _logger;
        private readonly ISecurityService _securityService;
        public ExtractedController(FirstDbContext context, ILoggerService logger, ISecurityService securityService)
        {
            _context = context;
            _logger = logger;
            _securityService = securityService;
        }

        [HttpPost("uploadcsv")]
        public async Task<IActionResult> UploadData(Cyphertext cyphertext, string WhichDatabase, int ProjectId)
        {
            // Decrypt the cyphertext
            string decryptedJson = _securityService.Decrypt(cyphertext.cyphertextt);

            // Deserialize the decrypted JSON into the List<Dictionary<string, string>> object
            List<Dictionary<string, string>> parsedData = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(decryptedJson);

            // Validate the list of dictionaries
            if (parsedData == null || parsedData.Count == 0)
            {
                return BadRequest("No data received");
            }

            try
            {
                var extractedDataList = new List<ExtractedOMRData>();
                foreach (var row in parsedData)
                {
                    var extracteddata = new ExtractedOMRData
                    {
                        ProjectId = ProjectId,
                        ExtractedOmrData = JsonConvert.SerializeObject(row.Where(kv => kv.Key != "Barcode").ToDictionary(kv => kv.Key, kv => kv.Value)),
                        BarCode = row.ContainsKey("Barcode") ? row["Barcode"] : null,
                        Status = 1
                    };
                    extractedDataList.Add(extracteddata);
                }
                if (WhichDatabase == "Local")
                {
                    _context.ExtractedOMRDatas.AddRange(extractedDataList);
                    await _context.SaveChangesAsync();
                }
                var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    _logger.LogEvent($"Scanned Data Added for {ProjectId}", "OMR data", userId, WhichDatabase);
                }

                return Ok("Data uploaded successfully");
            }
           
            catch (DbUpdateException ex)
            {
                return StatusCode(500, $"An error occurred: {ex.InnerException?.Message ?? ex.Message}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An unexpected error occurred: {ex.Message}");
            }
        }

        [HttpDelete("Extracted")]
        public async Task<IActionResult> DeleteExtracted(string WhichDatabase, int ProjectId)
        {

            if (ProjectId <= 0)
            {
                return NotFound("ProjectId not Found");
            }

            try
            {
                if (WhichDatabase == "Local")
                {
                    var deletescanned = await _context.ExtractedOMRDatas
                        .Where(a => a.ProjectId == ProjectId).ToListAsync();

                    if (!deletescanned.Any())
                    {
                        return BadRequest("No data found for the ProjectId");
                    }

                    _context.ExtractedOMRDatas.RemoveRange(deletescanned);
                    var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                    if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                    {
                        _logger.LogEvent($"Extracted Data deleted for {ProjectId}", "OMR data", userId, WhichDatabase);
                    }
                    await _context.SaveChangesAsync();

                }
               
                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");

            }
        }
    }
}
