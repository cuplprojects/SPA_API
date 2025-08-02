using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Threading.Tasks;
using SPA.Models;
using System;
using SPA.Data;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SPA.Services;
using SPA.Models.NonDBModels;
using Microsoft.CodeAnalysis;
using Newtonsoft.Json.Linq;
using ClosedXML.Excel;
using Irony.Parsing;
using System.Drawing.Printing;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using MySql.Data.MySqlClient;
using Microsoft.Data.SqlClient;

namespace SPA.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class OMRDataController : ControllerBase
    {
        private readonly FirstDbContext _firstDbContext;
        private readonly SecondDbContext _secondDbContext;
        private readonly IChangeLogger _changeLogger;
        private readonly ISecurityService _securityService;
        private readonly DatabaseConnectionChecker _connectionChecker;
        private readonly ILoggerService _logger;
        private readonly object _dbContextLock = new object();

        public OMRDataController(FirstDbContext firstDbContext, SecondDbContext secondDbContext, IChangeLogger changeLogger, ISecurityService securityService, DatabaseConnectionChecker connectionChecker, ILoggerService logger)
        {
            _firstDbContext = firstDbContext;
            _secondDbContext = secondDbContext;
            _changeLogger = changeLogger;
            _securityService = securityService;
            _connectionChecker = connectionChecker;
            _logger = logger;
        }

        private string GetFilePath(string fileName, string WhichDatabase)
        {
            return Path.Combine(GetDirectoryPath(WhichDatabase), fileName);
        }

        private string GetDirectoryPath(string WhichDatabase)
        {
            return WhichDatabase == "Local" ? "uploads" : "wwwroot/images";
        }


        [HttpGet]
        public async Task<IActionResult> GetRollNumbersAsync(string WhichDatabase, int ProjectId)
        {
            var rollNumberInfoList = new List<RollNumberInfo>();

            if (WhichDatabase == "Local")
            {
                var omrdatalist = await _firstDbContext.OMRdatas
                    .Where(o => o.ProjectId == ProjectId && o.Status == 1)
                    .ToListAsync();
                var correctedomrdatalist = await _firstDbContext.CorrectedOMRDatas
                    .Where(o => o.ProjectId == ProjectId)
                    .ToListAsync();
                var absenteeList = await _firstDbContext.Absentees
                    .Where(o => o.ProjectID == ProjectId)
                    .ToListAsync();

                rollNumberInfoList.AddRange(omrdatalist.Select(o => new RollNumberInfo { PrimaryKey = o.OmrDataId, RollNumber = ExtractRollNumberFromJson(o.OmrData) }));
                rollNumberInfoList.AddRange(correctedomrdatalist.Select(o => new RollNumberInfo { PrimaryKey = o.CorrectedId, RollNumber = ExtractRollNumberFromJson(o.CorrectedOmrData) }));
                rollNumberInfoList.AddRange(absenteeList.Select(a => new RollNumberInfo { PrimaryKey = a.AbsenteeId, RollNumber = a.RollNo }));
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                var omrdatalist = await _secondDbContext.OMRdatas
                    .Where(o => o.ProjectId == ProjectId && o.Status == 1)
                    .ToListAsync();
                var correctedomrdatalist = await _secondDbContext.CorrectedOMRDatas
                    .Where(o => o.ProjectId == ProjectId)
                    .ToListAsync();
                var absenteeList = await _secondDbContext.Absentees
                    .Where(o => o.ProjectID == ProjectId)
                    .ToListAsync();

                rollNumberInfoList.AddRange(omrdatalist.Select(o => new RollNumberInfo { PrimaryKey = o.OmrDataId, RollNumber = ExtractRollNumberFromJson(o.OmrData) }));
                rollNumberInfoList.AddRange(correctedomrdatalist.Select(o => new RollNumberInfo { PrimaryKey = o.CorrectedId, RollNumber = ExtractRollNumberFromJson(o.CorrectedOmrData) }));
                rollNumberInfoList.AddRange(absenteeList.Select(a => new RollNumberInfo { PrimaryKey = a.AbsenteeId, RollNumber = a.RollNo }));
            }

            // Save roll numbers and primary keys to Excel
            var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Roll Numbers");

            worksheet.Cell(1, 1).Value = "Primary Key";
            worksheet.Cell(1, 2).Value = "Roll Number";

            for (int i = 0; i < rollNumberInfoList.Count; i++)
            {
                worksheet.Cell(i + 2, 1).Value = rollNumberInfoList[i].PrimaryKey;
                worksheet.Cell(i + 2, 2).Value = rollNumberInfoList[i].RollNumber;
            }

            // Save the Excel file to a memory stream
            using (var stream = new MemoryStream())
            {
                workbook.SaveAs(stream);
                var content = stream.ToArray();

                // Return the Excel file as a response
                return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "RollNumbers.xlsx");
            }
        }

        private string ExtractRollNumberFromJson(string jsonData)
        {
            var jsonObject = JObject.Parse(jsonData);
            return jsonObject["Roll Number"]?.ToString();
        }

        [HttpDelete("Scanned")]
        public async Task<IActionResult> DeleteScanned(string WhichDatabase, int ProjectId)
        {

            if (ProjectId <= 0)
            {
                return NotFound("ProjectId not Found");
            }

            try
            {
                if (WhichDatabase == "Local")
                {
                    var deletescanned = await _firstDbContext.OMRdatas
                        .Where(a => a.ProjectId == ProjectId).ToListAsync();
                    var correctedscanned = await _firstDbContext.CorrectedOMRDatas.Where(a=>a.ProjectId == ProjectId).ToListAsync();
                    var flags = await _firstDbContext.Flags.
                        Where(a => a.ProjectId == ProjectId).ToListAsync();

                    if (!deletescanned.Any())
                    {
                        return BadRequest("No data found for the ProjectId");
                    }

                    _firstDbContext.OMRdatas.RemoveRange(deletescanned);
                    _firstDbContext.CorrectedOMRDatas.RemoveRange(correctedscanned);
                    _firstDbContext.Flags.RemoveRange(flags);
                    var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                    if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                    {
                        _logger.LogEvent($"Scanned Data deleted for {ProjectId}", "OMR data", userId, WhichDatabase);
                    }
                    await _firstDbContext.SaveChangesAsync();

                }
                else
                {
                    if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                    {
                        return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                    }
                    var deletescanned = await _secondDbContext.OMRdatas
                        .Where(a => a.ProjectId == ProjectId).ToListAsync();

                    if (!deletescanned.Any())
                    {
                        return BadRequest("No data found for the ProjectId");
                    }

                    _secondDbContext.OMRdatas.RemoveRange(deletescanned);

                    var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                    if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                    {
                        _logger.LogEvent($"Scanned Data deleted for {ProjectId}", "OMR data", userId, WhichDatabase);
                    }
                    await _secondDbContext.SaveChangesAsync();
                }
                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");

            }
        }

        [HttpDelete("Images")]
        public async Task<IActionResult> DeleteImages(string WhichDatabase, int ProjectId)
        {

            if (ProjectId <= 0)
            {
                return NotFound("ProjectId not Found");
            }

            try
            {
                if (WhichDatabase == "Local")
                {
                    var deleteImages = await _firstDbContext.OMRImages
                        .Where(a => a.ProjectId == ProjectId).ToListAsync();

                    if (!deleteImages.Any())
                    {
                        return BadRequest("No Images found for the ProjectId");
                    }

                    _firstDbContext.OMRImages.RemoveRange(deleteImages);
                    var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                    if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                    {
                        _logger.LogEvent($"OMR Images deleted for {ProjectId}", "OMR Images", userId, WhichDatabase);
                    }
                    await _firstDbContext.SaveChangesAsync();

                }
                else
                {
                    if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                    {
                        return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                    }
                    var deleteImages = await _secondDbContext.OMRImages
                        .Where(a => a.ProjectId == ProjectId).ToListAsync();

                    if (!deleteImages.Any())
                    {
                        return BadRequest("No Images found for the ProjectId");
                    }


                    _secondDbContext.OMRImages.RemoveRange(deleteImages);
                    var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                    if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                    {
                        _logger.LogEvent($"OMR Images deleted for {ProjectId}", "OMR Images", userId, WhichDatabase);
                    }
                    await _secondDbContext.SaveChangesAsync();
                }
                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");

            }
        }


        [HttpGet("omrdata/{projectId}/total-images")]
        public async Task<ActionResult<int>> GetTotalOmrImagesCount(int projectId, string WhichDatabase)
        {

            try
            {
                int totalCount = 0;

                if (WhichDatabase == "Local")
                {
                    // Query the first database context for total images count
                    totalCount = await _firstDbContext.OMRImages
                    .Where(od => od.ProjectId == projectId)
                    .CountAsync();
                }

                else
                {
                    if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                    {
                        return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                    }
                    // Query the second database context for total images count
                    totalCount = await _secondDbContext.OMRImages
                          .Where(od => od.ProjectId == projectId)
                          .CountAsync();
                }

                return Ok(totalCount);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }


        [HttpGet("count/{projectId}")]
        public async Task<ActionResult<int>> GetTotalUploadedDataCount(int projectId, string WhichDatabase)
        {
            try
            {
                int totalCount = 0;

                if (WhichDatabase == "Local")
                {
                    // Query to count total OMR data records in the first database context
                    totalCount = await _firstDbContext.OMRdatas
                       .Where(od => od.ProjectId == projectId)
                       .CountAsync();
                }

                else
                {
                    if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                    {
                        return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                    }
                    // Query to count total OMR data records in the second database context
                    totalCount = await _secondDbContext.OMRdatas
                        .Where(od => od.ProjectId == projectId)
                        .CountAsync();
                }

                return Ok(totalCount);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
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
                foreach (var row in parsedData)
                {
                    var omrData = new OMRdata
                    {
                        OmrDataId = GetNextScannedId(WhichDatabase),
                        ProjectId = ProjectId,
                        OmrData = JsonConvert.SerializeObject(row.Where(kv => kv.Key != "Barcode").ToDictionary(kv => kv.Key, kv => kv.Value)),
                        BarCode = row.ContainsKey("Barcode") ? row["Barcode"] : null,
                        Status = 1
                    };

                    try
                    {
                        // Detach any existing tracked entity with the same OmrDataId
                        if (WhichDatabase == "Local")
                        {
                            var existingEntity = _firstDbContext.OMRdatas.Local
                                .FirstOrDefault(e => e.OmrDataId == omrData.OmrDataId);

                            if (existingEntity != null)
                            {
                                _firstDbContext.Entry(existingEntity).State = EntityState.Detached;
                            }

                            _firstDbContext.OMRdatas.Add(omrData);
                            await _firstDbContext.SaveChangesAsync();
                        }
                        else
                        {
                            if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                            {
                                return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                            }

                            var existingEntity = _secondDbContext.OMRdatas.Local
                                .FirstOrDefault(e => e.OmrDataId == omrData.OmrDataId);

                            if (existingEntity != null)
                            {
                                _secondDbContext.Entry(existingEntity).State = EntityState.Detached;
                            }

                            _secondDbContext.OMRdatas.Add(omrData);
                            await _secondDbContext.SaveChangesAsync();
                        }
                    }
                    catch (DbUpdateException ex) when (ex.InnerException is MySqlConnector.MySqlException mysqlEx && mysqlEx.Number == 1062)
                    {
                        // Handle duplicate entry errors (MySQL error code 1062)
                        Console.WriteLine($"Duplicate entry found for OMRDataId: {omrData.OmrDataId}. Skipping this entry.");
                        continue;
                    }
                    catch (Exception ex)
                    {
                        // Log the unexpected exception and continue with the next row
                        Console.WriteLine($"An unexpected error occurred: {ex.Message}");
                        continue;
                    }
                }

                // Log the event after processing all rows
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

        [AllowAnonymous]
        [HttpPost("upload-batch")]
        public async Task<IActionResult> UploadBatch(int ProjectId, IList<IFormFile> files, string WhichDatabase)
        {
            // Validate files
            if (files == null || files.Count == 0)
            {
                return BadRequest("No files uploaded.");
            }

            if (WhichDatabase == "Local")
            {
                // Handle the "Local" database context
                var project = await _firstDbContext.Projects.FirstOrDefaultAsync(p => p.ProjectId == ProjectId);
                if (project == null)
                {
                    return BadRequest("Project not found in the local database.");
                }

                string projectDirectory = Path.Combine("wwwroot","projects",(project.ProjectId).ToString());
                string PD = Path.Combine("projects", (project.ProjectId).ToString());
                EnsureDirectoryExists(projectDirectory);

                List<string> failedFiles = new List<string>();
                List<string> uploadedFiles = new List<string>();

                var existingFileNames = await _firstDbContext.OMRImages
                    .Where(img => img.ProjectId == ProjectId)
                    .Select(img => img.OMRImagesName)
                    .ToListAsync();

                foreach (var file in files)
                {
                    string uniqueFileName = file.FileName;
                    string FileName = Path.GetFileNameWithoutExtension(file.FileName);

                    if (existingFileNames.Contains(uniqueFileName))
                    {
                        failedFiles.Add(file.FileName + $": Duplicate entry for with value '{uniqueFileName}'");
                        continue;
                    }

                    string filePath = Path.Combine(projectDirectory, uniqueFileName);
                    string filePath2 = Path.Combine(PD, uniqueFileName);

                    try
                    {
                        await SaveFile(file, filePath);

                        var newFile = new OMRImage
                        {
                            OMRImagesName = FileName,
                            FilePath = filePath2,
                            ProjectId = ProjectId
                        };

                        _firstDbContext.OMRImages.Add(newFile);
                        await _firstDbContext.SaveChangesAsync();

                        uploadedFiles.Add(file.FileName);
                    }
                    catch (Exception ex)
                    {
                        failedFiles.Add(file.FileName + ": " + ex.Message);
                    }
                }

                return GetUploadResponse("Local", uploadedFiles, failedFiles);
            }
            else if (WhichDatabase == "Online")
            {
                // Handle the "Online" database context
                var project = await _secondDbContext.Projects.FirstOrDefaultAsync(p => p.ProjectId == ProjectId);
                if (project == null)
                {
                    return BadRequest("Project not found in the online database.");
                }

                string projectDirectory = Path.Combine("wwwroot","projects",(project.ProjectId).ToString());
                string PD = Path.Combine("projects", (project.ProjectId).ToString());
                EnsureDirectoryExists(projectDirectory);

                List<string> failedFiles = new List<string>();
                List<string> uploadedFiles = new List<string>();

                var existingFileNames = await _secondDbContext.OMRImages
                    .Where(img => img.ProjectId == ProjectId)
                    .Select(img => img.OMRImagesName)
                    .ToListAsync();

                foreach (var file in files)
                {
                    string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file.FileName);
                    string uniqueFileName = $"{fileNameWithoutExtension}";

                    if (existingFileNames.Contains(uniqueFileName))
                    {
                        failedFiles.Add(file.FileName + $": Duplicate entry with value '{uniqueFileName}'");
                        continue;
                    }

                    string filePath = Path.Combine(projectDirectory, uniqueFileName);
                    string filePath2 = Path.Combine(PD, uniqueFileName);

                    try
                    {
                        await SaveFile(file, filePath);



                        var newFile = new OMRImage
                        {
                            OMRImagesName = uniqueFileName,
                            FilePath = filePath2,
                            ProjectId = ProjectId
                        };

                        _secondDbContext.OMRImages.Add(newFile);
                        await _secondDbContext.SaveChangesAsync();

                        uploadedFiles.Add(file.FileName);
                    }
                    catch (Exception ex)
                    {
                        failedFiles.Add(file.FileName + ": " + ex.Message);
                    }
                }

                return GetUploadResponse("Online", uploadedFiles, failedFiles);
            }
            else
            {
                return BadRequest("Invalid database selection.");
            }
        }

        private IActionResult GetUploadResponse(string contextName, List<string> uploadedFiles, List<string> failedFiles)
        {
            if (failedFiles.Count > 0)
            {
                return BadRequest(new
                {
                    message = $"Some files failed to upload in the {contextName} database.",
                    failedFiles,
                    uploadedFiles
                });
            }

            return Ok(new
            {
                message = $"Batch upload successful in the {contextName} database.",
                uploadedFiles
            });
        }

        private async Task SaveFile(IFormFile file, string filePath)
        {
            // Ensure the directory exists
            var directoryPath = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            // Save the file
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }
        }

        private void EnsureDirectoryExists(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
        }


      
        // modification required
        [HttpGet("omrdata/{projectId}/last-image-name")]
        public async Task<ActionResult<string>> GetLastOmrImageName(int projectId)
        {
            var lastOmrData = await _firstDbContext.OMRImages
                .Where(od => od.ProjectId == projectId)
                .OrderByDescending(od => od.OMRImagesID) // Assuming OmrDataId is auto-incremented
                .FirstOrDefaultAsync();

            if (lastOmrData == null)
            {
                return NotFound("No OMR data found for the specified project.");
            }

            return Ok(lastOmrData.OMRImagesName); // Assuming 'OmrImagesName' is the field you want
        }

        [HttpGet("OMRImagebyName")]
        public async Task<IActionResult> GetOMRImageAsync(string WhichDatabase, int ProjectId, string Name)
        {
            OMRImage oMRImage = new OMRImage();
            if (WhichDatabase == "Local")
            {
                oMRImage = await _firstDbContext.OMRImages.FirstOrDefaultAsync(u => u.ProjectId == ProjectId && u.OMRImagesName == Name);
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                oMRImage = await _secondDbContext.OMRImages.FirstOrDefaultAsync(u => u.ProjectId == ProjectId && u.OMRImagesName == Name);
            }

            if (oMRImage == null)
            {
                return NotFound("No Image Found");
            }

            return Ok(oMRImage);

        }

        private int GetNextScannedId(string WhichDatabase)
        {
            return WhichDatabase == "Local" ? _firstDbContext.OMRdatas.Max(c => (int?)c.OmrDataId) + 1 ?? 1 : _secondDbContext.OMRdatas.Max(c => (int?)c.OmrDataId) + 1 ?? 1;

        }
        private int GetNextImageId(string WhichDatabase)
        {
            return WhichDatabase == "Local" ? _firstDbContext.OMRImages.Max(c => (int?)c.OMRImagesID) + 1 ?? 1 : _secondDbContext.OMRImages.Max(c => (int?)c.OMRImagesID) + 1 ?? 1;
        }

    }

    public class RollNumberInfo
    {
        public int PrimaryKey { get; set; }
        public string RollNumber { get; set; }
    }
}


