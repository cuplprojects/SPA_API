using DocumentFormat.OpenXml.Office2010.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using OfficeOpenXml;
using SPA.Data;
using SPA.Services;
using System.Drawing.Printing;
using System.Security.Claims;
using System.Text.Json;

namespace SPA.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class KeyController : ControllerBase
    {
        private readonly FirstDbContext _firstDbContext;
        private readonly SecondDbContext _secondDbContext;
        private readonly IChangeLogger _changeLogger;
        private readonly DatabaseConnectionChecker _connectionChecker;
        private readonly ILoggerService _logger;

        public KeyController(FirstDbContext firstDbcontext, SecondDbContext secondDbContext, IChangeLogger changeLogger, DatabaseConnectionChecker connectionChecker, ILoggerService logger)
        {
            _firstDbContext = firstDbcontext;
            _secondDbContext = secondDbContext;
            _changeLogger = changeLogger;
            _connectionChecker = connectionChecker;
            _logger = logger;
        }




        [HttpGet("counts")]
        public async Task<ActionResult<IEnumerable<object>>> GetCounts(string WhichDatabase, int projectId)
        {
            try
            {
                List<object> counts = new List<object>();

                if (WhichDatabase == "Local")
                {
                    var keyss = await _firstDbContext.Keyss
                        .Where(x => x.ProjectId == projectId)
                        .GroupBy(x => x.CourseName)
                        .Select(group => new { CourseName = group.Key, Count = group.Count() })
                        .ToListAsync();

                    counts.AddRange(keyss);
                }
                else
                {
                    if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                    {
                        return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                    }
                    var keyss = await _secondDbContext.Keyss
                        .Where(x => x.ProjectId == projectId)
                        .GroupBy(x => x.CourseName)
                        .Select(group => new { CourseName = group.Key, Count = group.Count() })
                        .ToListAsync();

                    counts.AddRange(keyss);
                }

                return Ok(counts);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Failed to retrieve count: {ex.Message}");
            }
        }




        [HttpPost("upload")]
        public async Task<IActionResult> Upload(string WhichDatabase, int ProjectId, string courseName)
        {

            var file = Request.Form.Files[0];
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded.");
            }
            if (WhichDatabase == "Local")
            {
                try
                {
                    using (var stream = new System.IO.MemoryStream())
                    {
                        await file.CopyToAsync(stream);
                        stream.Position = 0; // Reset the stream position

                        var data = new List<Dictionary<string, object>>();

                        using (var package = new ExcelPackage(stream))
                        {
                            var worksheet = package.Workbook.Worksheets[0];

                            // Read header row to get set names
                            var headerRow = new List<string>();
                            for (int col = 2; col <= worksheet.Dimension.End.Column; col++)
                            {
                                headerRow.Add(worksheet.Cells[1, col].Text.Replace("\"", "").Trim());
                            }

                            // Create a dictionary to hold each set's data
                            var setsData = new Dictionary<string, List<Dictionary<string, string>>>();

                            // Initialize lists for each set in the dictionary
                            foreach (var header in headerRow)
                            {
                                setsData[header] = new List<Dictionary<string, string>>();
                            }

                            // Iterate through rows and columns to populate data for each set
                            for (int row = 2; row <= worksheet.Dimension.End.Row; row++)
                            {
                                var questionNo = worksheet.Cells[row, 1].Text.Replace("\"", "").Trim();

                                for (int col = 2; col <= worksheet.Dimension.End.Column; col++)
                                {
                                    var setName = headerRow[col - 2];
                                    var answer = worksheet.Cells[row, col].Text.Replace("\"", "").Trim();

                                    // Only add row data if there's a valid answer
                                    if (!string.IsNullOrEmpty(answer))
                                    {
                                        var rowData = new Dictionary<string, string>
                    {
                        { "QuestionNo", questionNo },
                        { "Answer", answer }
                    };
                                        setsData[setName].Add(rowData);
                                    }
                                }
                            }

                            // Combine data into a single list of dictionaries
                            foreach (var setName in setsData.Keys)
                            {
                                data.Add(new Dictionary<string, object>
            {
                { "Set", setName },
                { "Questions", setsData[setName] }
            });
                            }
                        }

                        var score = new Keys
                        {
                            ProjectId = ProjectId,
                            KeyData = System.Text.Json.JsonSerializer.Serialize(data),
                            CourseName = courseName,
                        };

                        // Save data to the database
                        await _firstDbContext.Keyss.AddAsync(score);


                        var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                        if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                        {
                            _logger.LogEvent($"Key Uploaded for {courseName}", "Keys", userId, WhichDatabase);
                        }
                        await _firstDbContext.SaveChangesAsync();

                        return Ok(new { message = "File uploaded successfully.", score });
                    }
                }
                catch (Exception ex)
                {
                    // Log the error details
                    Console.WriteLine($"Internal server error: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                    }
                    return StatusCode(500, $"Internal server error: {ex.Message}");
                }
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                try
                {
                    using (var stream = new System.IO.MemoryStream())
                    {
                        await file.CopyToAsync(stream);
                        stream.Position = 0; // Reset the stream position

                        var data = new List<Dictionary<string, object>>();

                        using (var package = new ExcelPackage(stream))
                        {
                            var worksheet = package.Workbook.Worksheets[0];

                            // Read header row to get set names
                            var headerRow = new List<string>();
                            for (int col = 2; col <= worksheet.Dimension.End.Column; col++)
                            {
                                headerRow.Add(worksheet.Cells[1, col].Text.Replace("\"", "").Trim());
                            }

                            // Create a dictionary to hold each set's data
                            var setsData = new Dictionary<string, List<Dictionary<string, string>>>();

                            // Initialize lists for each set in the dictionary
                            foreach (var header in headerRow)
                            {
                                setsData[header] = new List<Dictionary<string, string>>();
                            }

                            // Iterate through rows and columns to populate data for each set
                            for (int row = 2; row <= worksheet.Dimension.End.Row; row++)
                            {
                                var questionNo = worksheet.Cells[row, 1].Text.Replace("\"", "").Trim();

                                for (int col = 2; col <= worksheet.Dimension.End.Column; col++)
                                {
                                    var setName = headerRow[col - 2];
                                    var answer = worksheet.Cells[row, col].Text.Replace("\"", "").Trim();

                                    // Only add row data if there's a valid answer
                                    if (!string.IsNullOrEmpty(answer))
                                    {
                                        var rowData = new Dictionary<string, string>
                    {
                        { "QuestionNo", questionNo },
                        { "Answer", answer }
                    };
                                        setsData[setName].Add(rowData);
                                    }
                                }
                            }

                            // Combine data into a single list of dictionaries
                            foreach (var setName in setsData.Keys)
                            {
                                data.Add(new Dictionary<string, object>
            {
                { "Set", setName },
                { "Questions", setsData[setName] }
            });
                            }
                        }

                        var score = new Keys
                        {
                            KeyData = System.Text.Json.JsonSerializer.Serialize(data),
                            ProjectId = ProjectId,
                            CourseName = courseName,
                        };

                        // Save data to the database
                        await _secondDbContext.Keyss.AddAsync(score);
                        var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                        if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                        {
                            _logger.LogEvent($"Key Uploaded for {courseName}", "Keys", userId, WhichDatabase);
                        }
                        await _secondDbContext.SaveChangesAsync();

                        return Ok(new { message = "File uploaded successfully.", score });
                    }
                }
                catch (Exception ex)
                {
                    // Log the error details
                    Console.WriteLine($"Internal server error: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                    }
                    return StatusCode(500, $"Internal server error: {ex.Message}");
                }
            }
        }

        [HttpPut("updatekey")]
        public async Task<IActionResult> Update(string courseName, int ProjectId, string WhichDatabase)
        {
            var file = Request.Form.Files[0];
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded.");
            }

            if (WhichDatabase == "Local")
            {
                try
                {
                    // Determine which database to use
                    // Retrieve existing record
                    var existingRecord = await _firstDbContext.Keyss
                        .FirstOrDefaultAsync(k => k.ProjectId == ProjectId && k.CourseName == courseName);

                    if (existingRecord == null)
                    {
                        return NotFound($"No record found with ProjectId {ProjectId} and CourseName {courseName}.");
                    }

                    using (var stream = new System.IO.MemoryStream())
                    {
                        await file.CopyToAsync(stream);
                        stream.Position = 0; // Reset the stream position

                        var updatedData = new List<Dictionary<string, object>>();

                        using (var package = new ExcelPackage(stream))
                        {
                            var worksheet = package.Workbook.Worksheets[0];

                            // Read header row to get set names
                            var headerRow = new List<string>();
                            for (int col = 2; col <= worksheet.Dimension.End.Column; col++)
                            {
                                headerRow.Add(worksheet.Cells[1, col].Text.Replace("\"", "").Trim());
                            }

                            // Create a dictionary to hold each set's data
                            var setsData = new Dictionary<string, List<Dictionary<string, string>>>();

                            // Initialize lists for each set in the dictionary
                            foreach (var header in headerRow)
                            {
                                setsData[header] = new List<Dictionary<string, string>>();
                            }

                            // Iterate through rows and columns to populate data for each set
                            for (int row = 2; row <= worksheet.Dimension.End.Row; row++)
                            {
                                var questionNo = worksheet.Cells[row, 1].Text.Replace("\"", "").Trim();

                                for (int col = 2; col <= worksheet.Dimension.End.Column; col++)
                                {
                                    var setName = headerRow[col - 2];
                                    var answer = worksheet.Cells[row, col].Text.Replace("\"", "").Trim();

                                    // Only add row data if there's a valid answer
                                    if (!string.IsNullOrEmpty(answer))
                                    {
                                        var rowData = new Dictionary<string, string>
                            {
                                { "QuestionNo", questionNo },
                                { "Answer", answer }
                            };
                                        setsData[setName].Add(rowData);
                                    }
                                }
                            }

                            // Combine data into a single list of dictionaries
                            foreach (var setName in setsData.Keys)
                            {
                                updatedData.Add(new Dictionary<string, object>
                    {
                        { "Set", setName },
                        { "Questions", setsData[setName] }
                    });
                            }
                        }

                        // Update the existing record with new data
                        existingRecord.KeyData = System.Text.Json.JsonSerializer.Serialize(updatedData);


                        // Save changes to the database
                        await _firstDbContext.SaveChangesAsync();
                        var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                        if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                        {
                            _logger.LogEvent($"Key Updated for {courseName}", "Keys", userId, WhichDatabase);
                        }

                        return Ok(new { message = "Record updated successfully.", updatedRecord = existingRecord });
                    }
                }
                catch (Exception ex)
                {
                    // Log the error details
                    Console.WriteLine($"Internal server error: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                    }
                    return StatusCode(500, $"Internal server error: {ex.Message}");
                }
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                try
                {
                    // Determine which database to use
                    // Retrieve existing record
                    var existingRecord = await _secondDbContext.Keyss
                        .FirstOrDefaultAsync(k => k.ProjectId == ProjectId && k.CourseName == courseName);

                    if (existingRecord == null)
                    {
                        return NotFound($"No record found with ProjectId {ProjectId} and CourseName {courseName}.");
                    }

                    using (var stream = new System.IO.MemoryStream())
                    {
                        await file.CopyToAsync(stream);
                        stream.Position = 0; // Reset the stream position

                        var updatedData = new List<Dictionary<string, object>>();

                        using (var package = new ExcelPackage(stream))
                        {
                            var worksheet = package.Workbook.Worksheets[0];

                            // Read header row to get set names
                            var headerRow = new List<string>();
                            for (int col = 2; col <= worksheet.Dimension.End.Column; col++)
                            {
                                headerRow.Add(worksheet.Cells[1, col].Text.Replace("\"", "").Trim());
                            }

                            // Create a dictionary to hold each set's data
                            var setsData = new Dictionary<string, List<Dictionary<string, string>>>();

                            // Initialize lists for each set in the dictionary
                            foreach (var header in headerRow)
                            {
                                setsData[header] = new List<Dictionary<string, string>>();
                            }

                            // Iterate through rows and columns to populate data for each set
                            for (int row = 2; row <= worksheet.Dimension.End.Row; row++)
                            {
                                var questionNo = worksheet.Cells[row, 1].Text.Replace("\"", "").Trim();

                                for (int col = 2; col <= worksheet.Dimension.End.Column; col++)
                                {
                                    var setName = headerRow[col - 2];
                                    var answer = worksheet.Cells[row, col].Text.Replace("\"", "").Trim();

                                    // Only add row data if there's a valid answer
                                    if (!string.IsNullOrEmpty(answer))
                                    {
                                        var rowData = new Dictionary<string, string>
                            {
                                { "QuestionNo", questionNo },
                                { "Answer", answer }
                            };
                                        setsData[setName].Add(rowData);
                                    }
                                }
                            }

                            // Combine data into a single list of dictionaries
                            foreach (var setName in setsData.Keys)
                            {
                                updatedData.Add(new Dictionary<string, object>
                    {
                        { "Set", setName },
                        { "Questions", setsData[setName] }
                    });
                            }
                        }

                        // Update the existing record with new data
                        existingRecord.KeyData = System.Text.Json.JsonSerializer.Serialize(updatedData);

                        // Save changes to the database
                        await _secondDbContext.SaveChangesAsync();
                        var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                        if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                        {
                            _logger.LogEvent($"Key Updated for {courseName}", "Keys", userId, WhichDatabase);
                        }

                        return Ok(new { message = "Record updated successfully.", updatedRecord = existingRecord });
                    }
                }
                catch (Exception ex)
                {
                    // Log the error details
                    Console.WriteLine($"Internal server error: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                    }
                    return StatusCode(500, $"Internal server error: {ex.Message}");
                }
            }
        }

        private int GetNextKeyId(string whichDatabase)
        {
            return whichDatabase == "Local" ? _firstDbContext.Keyss.Max(c => (int?)c.KeyId) + 1 ?? 1 : _secondDbContext.Keyss.Max(c => (int?)c.KeyId) + 1 ?? 1;
        }

    }


}

