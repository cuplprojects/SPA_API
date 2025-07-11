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
            Console.WriteLine($"File length: {(file?.Length ?? 0)}");
            if (file == null || file.Length == 0)
            {
                Console.WriteLine("No file uploaded.");
                return BadRequest("No file uploaded.");
            }

            var key = await _firstDbContext.Keyss
                .FirstOrDefaultAsync(k => k.ProjectId == ProjectId && k.CourseName == courseName);
            var score = await _firstDbContext.Scores
                .FirstOrDefaultAsync(k => k.ProjectId == ProjectId && k.CourseName == courseName);
            if (key != null)
            {
               _firstDbContext.Keyss.Remove(key);
            }

            var subjectRangesJson = Request.Form["subjectRanges"];
            Console.WriteLine($"Received subjectRanges JSON: {subjectRangesJson}");
            if (string.IsNullOrEmpty(subjectRangesJson))
            {
                Console.WriteLine("Subject ranges are missing.");
                return BadRequest("Subject ranges are required.");
            }

            Dictionary<string, Dictionary<string, int>> subjectRanges;
            try
            {
                subjectRanges = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, int>>>(subjectRangesJson);
                Console.WriteLine($"Parsed subjectRanges keys: {string.Join(", ", subjectRanges.Keys)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to deserialize subjectRanges: {ex.Message}");
                return BadRequest($"Invalid subject ranges format: {ex.Message}");
            }

            try
            {
                using (var stream = new MemoryStream())
                {
                    await file.CopyToAsync(stream);
                    stream.Position = 0;

                    var subjectData = new Dictionary<string, List<Dictionary<string, object>>>();
                    var lowerSubjectMap = subjectRanges.Keys.ToDictionary(k => k.ToLowerInvariant(), v => v);
                    foreach (var subj in subjectRanges.Keys)
                        subjectData[subj] = new List<Dictionary<string, object>>();

                    using (var package = new ExcelPackage(stream))
                    {
                        var worksheet = package.Workbook.Worksheets[0];
                        Console.WriteLine($"Worksheet dimensions: Rows={worksheet.Dimension.End.Row}, Columns={worksheet.Dimension.End.Column}");

                        var subjectColumnRanges = new Dictionary<string, List<int>>();
                        string currentSubject = null;

                        // Improved subject column grouping
                        for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
                        {
                            var subjectHeader = worksheet.Cells[1, col].Text.Trim();
                            if (!string.IsNullOrEmpty(subjectHeader))
                            {
                                var subjectKeyLower = subjectHeader.ToLowerInvariant();
                                if (lowerSubjectMap.ContainsKey(subjectKeyLower))
                                {
                                    currentSubject = lowerSubjectMap[subjectKeyLower];
                                    if (!subjectColumnRanges.ContainsKey(currentSubject))
                                        subjectColumnRanges[currentSubject] = new List<int>();
                                }
                            }

                            if (!string.IsNullOrEmpty(currentSubject))
                            {
                                subjectColumnRanges[currentSubject].Add(col);
                                Console.WriteLine($"  Added column {col} to subject '{currentSubject}'");
                            }
                        }

                        foreach (var subject in subjectRanges.Keys)
                        {
                            Console.WriteLine($"Processing subject '{subject}'");

                            var startQuestion = subjectRanges[subject]["start"];
                            var endQuestion = subjectRanges[subject]["end"];
                            Console.WriteLine($"  Question range: {startQuestion} to {endQuestion}");

                            if (!subjectColumnRanges.ContainsKey(subject))
                            {
                                Console.WriteLine($"  No columns found for subject '{subject}', skipping");
                                continue;
                            }

                            var columns = subjectColumnRanges[subject];
                            Console.WriteLine($"  Columns for subject: {string.Join(", ", columns)}");

                            var setCodes = new Dictionary<int, string>();
                            foreach (var col in columns)
                            {
                                var setCode = worksheet.Cells[2, col].Text.Trim();
                                Console.WriteLine($"    Set code at row 2, col {col}: '{setCode}'");
                                if (!string.IsNullOrEmpty(setCode))
                                {
                                    setCodes[col] = setCode;
                                }
                            }

                            var setsData = new Dictionary<string, List<Dictionary<string, string>>>();
                            foreach (var setCode in setCodes.Values)
                                setsData[setCode] = new List<Dictionary<string, string>>();

                            int excelStartRow = 3;
                            int currentQuestionNo = startQuestion;

                            for (int excelRow = excelStartRow; excelRow <= worksheet.Dimension.End.Row && currentQuestionNo <= endQuestion; excelRow++)
                            {
                                foreach (var kvp in setCodes)
                                {
                                    int col = kvp.Key;
                                    string setCode = kvp.Value;

                                    var answer = worksheet.Cells[excelRow, col].Text.Trim();
                                    if (!string.IsNullOrEmpty(answer))
                                    {
                                        setsData[setCode].Add(new Dictionary<string, string>
                                {
                                    { "QuestionNo", currentQuestionNo.ToString() },
                                    { "Answer", answer }
                                });
                                    }
                                }

                                currentQuestionNo++;
                            }

                            foreach (var setCode in setsData.Keys)
                            {
                                if (setsData[setCode].Any())
                                {
                                    subjectData[subject].Add(new Dictionary<string, object>
                            {
                                { "Set", setCode },
                                { "Questions", setsData[setCode] }
                            });
                                    Console.WriteLine($"  Added set '{setCode}' data to subject '{subject}'");
                                }
                            }
                        }
                    }

                    var keys = new Keys
                    {
                        ProjectId = ProjectId,
                        KeyData = System.Text.Json.JsonSerializer.Serialize(subjectData),
                        CourseName = courseName,
                    };

                    await _firstDbContext.Keyss.AddAsync(keys);
                    if(score!= null) { 
                        _firstDbContext.Scores.Remove(score);
                    }

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
                Console.WriteLine($"Internal server error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        private int GetNextKeyId(string whichDatabase)
        {
            return whichDatabase == "Local" ? _firstDbContext.Keyss.Max(c => (int?)c.KeyId) + 1 ?? 1 : _secondDbContext.Keyss.Max(c => (int?)c.KeyId) + 1 ?? 1;
        }

    }


}

