using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using SPA.Data;
using SPA.Models;
using SPA.Models.NonDBModels;
using System;
using System.Collections.Generic;
using System.Linq;
using SPA.Models;
using Microsoft.CodeAnalysis;
using System.ComponentModel.DataAnnotations.Schema;
using SPA.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
namespace SPA.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ScoreController : ControllerBase
    {
        private readonly FirstDbContext _firstDbContext;
        private readonly SecondDbContext _secondDbContext;
        private readonly ISecurityService _securityService;
        private readonly IChangeLogger _changeLogger;
        private readonly DatabaseConnectionChecker _connectionChecker;

        public ScoreController(FirstDbContext firstDbContext, SecondDbContext secondDbContext, ISecurityService securityService, IChangeLogger changeLogger, DatabaseConnectionChecker connectionChecker)
        {
            _firstDbContext = firstDbContext;
            _secondDbContext = secondDbContext;
            _securityService = securityService;
            _changeLogger = changeLogger;
            _connectionChecker = connectionChecker;
        }

        [HttpGet]
        public async Task<IActionResult> GetScore(string WhichDatabase, int ProjectId, string courseName)
        {
            try
            {
                IQueryable<Score> scoresQuery;

                if (WhichDatabase == "Local")
                {
                    scoresQuery = _firstDbContext.Scores.AsQueryable();
                }
                else
                {
                    if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                    {
                        return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                    }
                    scoresQuery = _secondDbContext.Scores.AsQueryable();
                }

                // Filter by ProjectId
                scoresQuery = scoresQuery.Where(s => s.ProjectId == ProjectId);

                // Filter by CourseName if provided
                if (!string.IsNullOrEmpty(courseName))
                {
                    scoresQuery = scoresQuery.Where(s => s.CourseName == courseName);
                }

                var scores = await scoresQuery.ToListAsync();

                /*string encryptedDataToSend = _securityService.Encrypt(JsonConvert.SerializeObject(scores));*/

                return Ok(scores);
            }
            catch (DbUpdateException dbEx)
            {
                return StatusCode(500, $"Database error occurred: {dbEx.Message}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Failed to retrieve scores: {ex.Message}");
            }
        }


        [HttpGet("count")]
        public async Task<ActionResult<IEnumerable<object>>> GetCount(string WhichDatabase, int projectId)
        {
            try
            {
                List<object> counts = new List<object>();

                if (WhichDatabase == "Local")
                {
                    var scores = await _firstDbContext.Scores
                        .Where(x => x.ProjectId == projectId)
                        .GroupBy(x => x.CourseName)
                        .Select(group => new { CourseName = group.Key, Count = group.Count() })
                        .ToListAsync();

                    counts.AddRange(scores);
                }
                else
                {
                    if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                    {
                        return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                    }
                    var scores = await _secondDbContext.Scores
                        .Where(x => x.ProjectId == projectId)
                        .GroupBy(x => x.CourseName)
                        .Select(group => new { CourseName = group.Key, Count = group.Count() })
                        .ToListAsync();

                    counts.AddRange(scores);
                }

                return Ok(counts);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Failed to retrieve count: {ex.Message}");
            }
        }

        private async Task<List<AmbiguousQue>> GetAmbiguousQuestionsAsync(int projectId,string courseName, string WhichDatabase)
        {
            if (WhichDatabase == "Local")
            {
                return await _firstDbContext.AmbiguousQues.Where(aq => aq.ProjectId == projectId && aq.Course == courseName).ToListAsync();

            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return null;
                }
                return await _secondDbContext.AmbiguousQues.Where(aq => aq.ProjectId == projectId && aq.Course == courseName).ToListAsync();
            }
        }


        [HttpGet("omrdata/{projectId}/details")]
        public async Task<ActionResult<object>> GetOMRDetails(int projectId, string courseName, string WhichDatabase)
        {
            int UserID = 0;
            var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                UserID = userId;
                Console.WriteLine($"UserID: {UserID}");
            }

            try
            {
                IQueryable<OMRdata> omrDataQuery;
                IQueryable<CorrectedOMRData> correctedOmrQuery;
                IQueryable<Keys> keyQuery;
                IQueryable<ResponseConfig> responseConfigQuery;
                IQueryable<Score> scoreQuery;
                IQueryable<AmbiguousQue> ambiguousQuesQuery;

                // Choose database context based on WhichDatabase parameter
                if (WhichDatabase.Equals("Local", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Using Local Database");
                    omrDataQuery = _firstDbContext.OMRdatas.Where(od => od.ProjectId == projectId && od.Status == 1);
                    correctedOmrQuery = _firstDbContext.CorrectedOMRDatas.Where(co => co.ProjectId == projectId);
                    keyQuery = _firstDbContext.Keyss.Where(k => k.ProjectId == projectId);
                    responseConfigQuery = _firstDbContext.ResponseConfigs.Where(u => u.ProjectId == projectId);
                    scoreQuery = _firstDbContext.Scores.Where(s => s.ProjectId == projectId);
                    ambiguousQuesQuery = _firstDbContext.AmbiguousQues.Where(s => s.ProjectId == projectId);
                }
                else
                {
                    Console.WriteLine("Using Online Database");
                    if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                    {
                        return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                    }
                    omrDataQuery = _secondDbContext.OMRdatas.Where(od => od.ProjectId == projectId && od.Status == 1);
                    correctedOmrQuery = _secondDbContext.CorrectedOMRDatas.Where(co => co.ProjectId == projectId);
                    keyQuery = _secondDbContext.Keyss.Where(k => k.ProjectId == projectId);
                    responseConfigQuery = _secondDbContext.ResponseConfigs.Where(u => u.ProjectId == projectId);
                    scoreQuery = _secondDbContext.Scores.Where(s => s.ProjectId == projectId);
                    ambiguousQuesQuery = _secondDbContext.AmbiguousQues.Where(_co => _co.ProjectId == projectId);
                }

                Console.WriteLine("Fetching data from the database...");
                var omrDataList = await omrDataQuery.Select(u => u.OmrData).ToListAsync();
                var correctedOmrList = await correctedOmrQuery.Select(u => u.CorrectedOmrData).ToListAsync();
                var keys = await keyQuery.ToListAsync();
                var responseConfigs = await responseConfigQuery.ToListAsync();
                var existingScores = await scoreQuery.ToListAsync();

                if (!omrDataList.Any() || !correctedOmrList.Any() || !keys.Any() || !responseConfigs.Any())
                {
                    Console.WriteLine("Required data not found.");
                    return NotFound("Required data not found.");
                }

                Console.WriteLine("Combining OMR data...");
                var resultsList = new List<object>();
                var combinedOmrDataList = omrDataList.Concat(correctedOmrList).ToList();

                foreach (var omrData in combinedOmrDataList)
                {
                    JObject omrDataObject = JObject.Parse(omrData);
                    string rollNumber = (string)omrDataObject["Roll Number"];

                    Console.WriteLine($"Processing OMR data for Roll Number: {rollNumber}");

                    if (existingScores.Any(s => s.RollNumber.Equals(rollNumber, StringComparison.OrdinalIgnoreCase)))
                    {
                        Console.WriteLine($"Skipping Roll Number {rollNumber} as score already exists.");
                        continue; // Skip processing if score already exists for this roll number
                    }

                    // Fetch Registration Data
                    var registrationData = WhichDatabase.Equals("Local", StringComparison.OrdinalIgnoreCase)
                        ? await _firstDbContext.RegistrationDatas.FirstOrDefaultAsync(rd => rd.RollNumber == rollNumber && rd.ProjectId == projectId)
                        : await _secondDbContext.RegistrationDatas.FirstOrDefaultAsync(rd => rd.RollNumber == rollNumber && rd.ProjectId == projectId);

                    Console.WriteLine(registrationData == null ? $"No registration data for Roll Number {rollNumber}" : $"Found registration data for Roll Number {rollNumber}");

                    List<string> subjectCodes = new List<string>();
                    if (registrationData != null)
                    {
                        try
                        {
                            var subjectCodeToken = JObject.Parse(registrationData.RegistrationsData)["Subject Code"];
                            if (subjectCodeToken is JArray subjectCodeArray)
                            {
                                subjectCodes = subjectCodeArray.ToObject<List<string>>();
                            }
                            else if (subjectCodeToken is JValue subjectCodeValue && subjectCodeValue.Type == JTokenType.String)
                            {
                                subjectCodes = new List<string> { subjectCodeValue.ToString() };
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error parsing 'Subject Code': {ex.Message}");
                        }
                    }

                    Console.WriteLine($"Subject Codes: {string.Join(", ", subjectCodes)}");

                    var hasSubjectCode = subjectCodes.Any();
                    var courseNameMatches = hasSubjectCode && subjectCodes.Contains(courseName, StringComparer.OrdinalIgnoreCase);
                    var matchedResponseConfigs = responseConfigs.Where(r => r.CourseName.Equals(courseName, StringComparison.OrdinalIgnoreCase)).ToList();

                    if (registrationData == null)
                    {
                        Console.WriteLine("Case 3: No Registration Data");
                        if (!matchedResponseConfigs.Any())
                        {
                            Console.WriteLine("No matching response config found.");
                            continue; // Skip if no matching response config found
                        }

                        foreach (var responseConfig in matchedResponseConfigs)
                        {
                            await ProcessOmrData(omrDataObject, responseConfig, keys, resultsList, projectId, WhichDatabase, UserID, courseName);
                        }
                    }
                    else if (!hasSubjectCode)
                    {
                        Console.WriteLine("Case 1: No Subject Code in Registration Data");
                        if (!matchedResponseConfigs.Any())
                        {
                            Console.WriteLine("No matching response config found.");
                            continue; // Skip if no matching response config found
                        }

                        foreach (var responseConfig in matchedResponseConfigs)
                        {
                            await ProcessOmrData(omrDataObject, responseConfig, keys, resultsList, projectId, WhichDatabase, UserID, courseName);
                        }
                    }
                    else if (courseNameMatches)
                    {
                        Console.WriteLine("Case 2: Multiple Subject Codes in Registration Data");
                        if (!matchedResponseConfigs.Any())
                        {
                            Console.WriteLine("No matching response config found.");
                            continue; // Skip if no matching response config found
                        }

                        foreach (var responseConfig in matchedResponseConfigs)
                        {
                            await ProcessOmrData(omrDataObject, responseConfig, keys, resultsList, projectId, WhichDatabase, UserID, courseName);
                        }
                    }
                }

                Console.WriteLine("Saving changes to the database...");
                if (WhichDatabase.Equals("Local", StringComparison.OrdinalIgnoreCase))
                {
                    await _firstDbContext.SaveChangesAsync();
                }
                else
                {
                    await _secondDbContext.SaveChangesAsync();
                }

                Console.WriteLine("Returning results...");
                return resultsList;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred: " + ex.Message);
            }
        }


        private async Task ProcessOmrData(JObject omrDataObject, ResponseConfig responseConfig, List<Keys> keys, List<object> resultsList, int projectId, string whichDatabase, int userID)
        {
            var matchingKey = keys.FirstOrDefault(k => k.CourseName == responseConfig.CourseName);
            if (matchingKey == null)
            {
                throw new Exception($"No matching key found for course name '{responseConfig.CourseName}'.");
            }
        
            string bookletSet = "";
            if ((whichDatabase.Equals("Local", StringComparison.OrdinalIgnoreCase)))
            {
                var fieldconfig = await _firstDbContext.FieldConfigs.FirstOrDefaultAsync(fc => fc.ProjectId == projectId && fc.FieldName == "Booklet Series");
                if (fieldconfig == null)
                {
                    bookletSet = "A";
                }
                else
                {
                    bookletSet = (string)omrDataObject["Booklet Series"];
                }
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    bookletSet = "A";
                }
                else
                {
                    var fieldconfig = await _secondDbContext.FieldConfigs.FirstOrDefaultAsync(fc => fc.ProjectId == projectId && fc.FieldName == "Booklet Series");
                    if (fieldconfig == null)
                    {
                        bookletSet = "A";
                    }
                    else
                    {
                        bookletSet = (string)omrDataObject["Booklet Series"];
                    }
                }
            }
            string courseName = responseConfig.CourseName;
            
            var sets = JsonConvert.DeserializeObject<List<Sets>>(matchingKey.KeyData);
            var setValues = sets.Select(s => s.Set).ToList();
            string matchedSet = setValues.FirstOrDefault(s => s.Trim().Last().ToString().Equals(bookletSet, StringComparison.OrdinalIgnoreCase));
            var ambiguousQuestions = await GetAmbiguousQuestionsAsync(projectId,courseName, whichDatabase);
            var ambquestion = ambiguousQuestions.Where(u => u.SetCode.Equals(bookletSet)).ToList();
        
            if (matchedSet == null)
            {
                Console.WriteLine($"Booklet Set '{bookletSet}' does not match with any of the sets.");
                return;
            }
        
            var matchedSetObject = sets.FirstOrDefault(s => s.Set == matchedSet);
            if (matchedSetObject == null)
            {
                throw new Exception($"Set '{matchedSet}' not found in sets list for project {projectId}");
            }
        
            string answersJsonString = (string)omrDataObject["Answers"];
            if (string.IsNullOrEmpty(answersJsonString))
            {
                throw new Exception("Answers field is missing or not in expected format.");
            }
        
            JObject answersObject;
            try
            {
                answersObject = JObject.Parse(answersJsonString);
            }
            catch (Exception ex)
            {
                throw new Exception("Error parsing Answers field: " + ex.Message);
            }
        
            var allQuestions = matchedSetObject.Questions;
            var sectionResults = new List<SectionResult>();
            double totalScore = 0;
        
            var sections = JsonConvert.DeserializeObject<List<Models.NonDBModels.Section>>(responseConfig.SectionsJson) ?? new List<Models.NonDBModels.Section>();
        
            foreach (var section in sections)
            {
                var sectionQuestions = allQuestions.Where(q =>
                {
                    if (int.TryParse(q.QuestionNo, out int questionNo))
                    {
                        return questionNo >= section.StartQuestion && questionNo <= section.EndQuestion;
                    }
                    return false;
                }).ToList();
        
        
                var sectionResultsData = CalculateResults(answersObject, sectionQuestions, section.MarksCorrect, section.MarksWrong, section.NegativeMarking, ambquestion);
        
                sectionResults.Add(new SectionResult
                {
                    SectionName = section.Name,
                    TotalCorrectAnswers = sectionResultsData.TotalCorrectAnswers,
                    TotalWrongAnswers = sectionResultsData.TotalWrongAnswers,
                    TotalScoreSub = sectionResultsData.TotalScore
                });
        
                totalScore += sectionResultsData.TotalScore;
            }
        
            var omrDetails = new
            {
                RollNumber = (string)omrDataObject["Roll Number"],
                CourseName = responseConfig.CourseName,
                BSet = bookletSet,
                SectionResults = sectionResults,
                TotalScore = totalScore
            };
        
            resultsList.Add(omrDetails);
        
            var score = new Score
            {
                ScoreId = GetNextScoreId(whichDatabase),
                TotalScore = omrDetails.TotalScore,
                CourseName = responseConfig.CourseName,
                ProjectId = projectId,
                RollNumber = omrDetails.RollNumber,
                ScoreData = JsonConvert.SerializeObject(sectionResults)
            };
        
            if (whichDatabase.Equals("Local", StringComparison.OrdinalIgnoreCase))
            {
                _firstDbContext.Scores.Add(score);
                string scoreJson = JsonConvert.SerializeObject(score);
                _changeLogger.LogForDBSync("Insert", "Scores", scoreJson, whichDatabase, userID);
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    throw new Exception("Online database is not available.");
                }
        
                _secondDbContext.Scores.Add(score);
                string scoreJson = JsonConvert.SerializeObject(score);
                _changeLogger.LogForDBSync("Insert", "Scores", scoreJson, whichDatabase, userID);
            }
        }





        private Results CalculateResults(
       JObject answersObject,
       List<Question> questions,
       double marksCorrect,
       double marksWrong,
       bool negativeMarking,
       List<AmbiguousQue> ambiguousQue)
            {
        int totalCorrectAnswers = 0;
        int totalWrongAnswers = 0;
    /*            int totalOptionE = 0;*/
        double totalScore = 0;
    
        // Lists to store ambiguous question numbers and their corresponding marking IDs
        List<int> ambiguousQuestionNumbers = new List<int>();
        Dictionary<int, int> ambiguousMarkingIds = new Dictionary<int, int>();
    
        // Populate the lists with data from ambiguousQueList
        if (ambiguousQueList != null)
        {
            foreach (var ambiguousQue in ambiguousQueList)
            {
                ambiguousQuestionNumbers.Add(ambiguousQue.QuestionNumber);
                ambiguousMarkingIds[ambiguousQue.QuestionNumber] = ambiguousQue.MarkingId;
            }
        }
    
        foreach (var question in questions)
        {
            string answerKey = question.QuestionNo.ToString();
            string userAnswer = answersObject[answerKey]?.ToString();
            string correctAnswer = question.Answer;
            var correctAnswerArray = correctAnswer.Split(',').Select(a=>a.Trim()).ToList();
    
            // Check if the question matches any of the ambiguous question numbers
            bool isAmbiguousQuestion = ambiguousQuestionNumbers.Contains(Int32.Parse(question.QuestionNo));
    
            // Apply MarkingId-based logic if it's an ambiguous question
            if (isAmbiguousQuestion)
            {
                int markingId = ambiguousMarkingIds[Int32.Parse(question.QuestionNo)];
                switch (markingId)
                {
                    case 1:
                        totalCorrectAnswers++;
                        continue;
                    case 2:
                        if (!string.IsNullOrEmpty(userAnswer))
                        {
                            totalCorrectAnswers++;
                        }
                        continue;
                    case 3:
                        // Don’t award marks to any candidate
                        continue;
                }
            }
            else
            {
                // Original scoring logic if no ambiguity or not an ambiguous question
                if (!string.IsNullOrEmpty(userAnswer))
                {
                    if (correctAnswerArray.Contains(userAnswer,StringComparer.OrdinalIgnoreCase))
                    {
                        totalCorrectAnswers++;
                    }
                    /*else if (userAnswer.Equals("e", StringComparison.OrdinalIgnoreCase))
                    {
                        totalOptionE++;
                        // Skip this question
                        continue;
                    }*/
                    else
                    {
                        totalWrongAnswers++;
                    }
                }
               /* else
                {
                    totalWrongAnswers++;
                }*/
            }
        }
    
        // Calculate the final score with or without negative marking
        totalScore += totalCorrectAnswers * marksCorrect;
        if (negativeMarking)
        {
            totalScore -= totalWrongAnswers * marksWrong;
        }
    
        return new Results
        {
            TotalCorrectAnswers = totalCorrectAnswers,
            TotalWrongAnswers = totalWrongAnswers,
    /*                TotalOptionE = totalOptionE,*/
            TotalScore = totalScore
        };
    }


        private class Results
        {
            public int TotalCorrectAnswers { get; set; }
            public int TotalWrongAnswers { get; set; }
            public double TotalScore { get; set; }
            public Dictionary<string, int> QuestionResults { get; set; } // Include question results
        }

        private class SectionResult
        {
            public string SectionName { get; set; }
            public int TotalCorrectAnswers { get; set; }
            public int TotalWrongAnswers { get; set; }
            public double TotalScoreSub { get; set; }
            public Dictionary<string, int> QuestionResults { get; set; } // Include question results
        }



        [HttpDelete]

        public async Task<IActionResult> DeleteScore(string WhichDatabase, int ProjectId, string CourseName)
        {
            int UserID = 0;
            var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                UserID = userId;
            }
            if (WhichDatabase == "Local")
            {
                var deletescore = await _firstDbContext.Scores.Where(a => a.ProjectId == ProjectId && a.CourseName == CourseName).ToListAsync();
                _firstDbContext.Scores.RemoveRange(deletescore);
                foreach (var score in deletescore)
                {
                    string scoreJson = JsonConvert.SerializeObject(score);
                    _changeLogger.LogForDBSync("Delete", "Scores", scoreJson, WhichDatabase, UserID);
                }
                await _firstDbContext.SaveChangesAsync();
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                var deletescore = await _secondDbContext.Scores.Where(a => a.ProjectId == ProjectId && a.CourseName == CourseName).ToListAsync();
                _secondDbContext.Scores.RemoveRange(deletescore);
                foreach (var score in deletescore)
                {
                    string scoreJson = JsonConvert.SerializeObject(score);
                    _changeLogger.LogForDBSync("Delete", "Scores", scoreJson, WhichDatabase, UserID);
                }
                await _secondDbContext.SaveChangesAsync();
            }
            return Ok();
        }

        private int GetNextScoreId(string WhichDatabase)
        {
            return WhichDatabase == "Local" ? _firstDbContext.Scores.Max(c => (int?)c.ScoreId) + 1 ?? 1 : _secondDbContext.Scores.Max(c => (int?)c.ScoreId) + 1 ?? 1;
        }
    }
}

