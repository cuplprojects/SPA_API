using SPA.Data;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SPA.Models.NonDBModels;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.IdentityModel.Tokens;
using System.Linq;
using System.Drawing.Printing;
using System.Security.Claims;
using Org.BouncyCastle.Bcpg;
using SPA.Models;

namespace SPA.Services
{
    public class FieldConfigService
    {
        private readonly FirstDbContext _FirstDbcontext;
        private readonly SecondDbContext _SecondDbContext;
        private readonly IChangeLogger _ChangeLogger;
        private readonly DatabaseConnectionChecker _connectionChecker;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public FieldConfigService(FirstDbContext context, SecondDbContext secondDbContext, IChangeLogger changeLogger, DatabaseConnectionChecker connectionChecker, IHttpContextAccessor httpContextAccessor)
        {
            _FirstDbcontext = context;
            _SecondDbContext = secondDbContext;
            _ChangeLogger = changeLogger;
            _connectionChecker = connectionChecker;
            _httpContextAccessor = httpContextAccessor;
        }


        public async Task<List<FieldConfig>> GetFieldConfigsAsync(string WhichDatabase, int ProjectId)
        {
            if (WhichDatabase == "Local")
            {
                return await _FirstDbcontext.FieldConfigs.Where(f =>f.ProjectId == ProjectId).ToListAsync();
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    throw new Exception("Connection Unavailable");
                }
                return await _SecondDbContext.FieldConfigs.Where(f => f.ProjectId == ProjectId).ToListAsync();
            }
        }

        public async Task CheckForMultipleResponsesAsync(
    List<OMRdata> omrDataList,
    List<CorrectedOMRData> correctedomrDataList,
    List<AmbiguousQue> ambiguousQueList,
    string fieldName,
    int ProjectId,
    string WhichDatabase)
        {
            try
            {
                Console.WriteLine("Checking for multiple responses...");
                // Pre-process response configurations for faster lookup

                // Group ambiguous questions by SetCode for efficient lookup
                var ambiguousQuestionsBySetCode = ambiguousQueList
                    .GroupBy(q => q.SetCode)
                    .ToDictionary(g => g.Key, g => g.ToList());

                // Process original OMR data
                foreach (var omr in omrDataList)
                {
                    await ProcessOMRDataAsync(omr.OmrData, omr.BarCode, ambiguousQuestionsBySetCode,
                         ProjectId, WhichDatabase);
                }

                // Process corrected OMR data
                foreach (var correctedOmr in correctedomrDataList)
                {
                    await ProcessOMRDataAsync(correctedOmr.CorrectedOmrData, correctedOmr.BarCode,
                        ambiguousQuestionsBySetCode, ProjectId, WhichDatabase);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error checking for multiple responses: {ex.Message}", ex);
            }
        }

        private async Task ProcessOMRDataAsync(
            string omrDataJson,
            string barCode,
            Dictionary<string, List<AmbiguousQue>> ambiguousQuestionsBySetCode,
            int ProjectId,
            string WhichDatabase)
        {
            var data = JsonConvert.DeserializeObject<Dictionary<string, string>>(omrDataJson);
            if (data == null)
            {
                Console.WriteLine($"Failed to deserialize OMR data for BarCode: {barCode}");
                return;
            }

            if (!data.TryGetValue("Booklet Series", out string bookletSeries))
            {
                Console.WriteLine($"Booklet Series not found for BarCode: {barCode}");
                return;
            }

            Console.WriteLine($"Processing Booklet Series: {bookletSeries} for BarCode: {barCode}");

            // Get relevant questions for this booklet series
            if (!ambiguousQuestionsBySetCode.TryGetValue(bookletSeries, out var relevantQuestions))
            {
                Console.WriteLine($"No ambiguous questions found for Booklet Series: {bookletSeries}");
                return;
            }

            if (!data.TryGetValue("Answers", out string answersJson))
            {
                Console.WriteLine($"Answers field missing for BarCode: {barCode}");
                return;
            }

            var answers = JsonConvert.DeserializeObject<Dictionary<string, string>>(answersJson);
            if (answers == null)
            {
                Console.WriteLine($"Failed to deserialize answers for BarCode: {barCode}");
                return;
            }

            // Process each ambiguous question
            var flagsToCreate = new List<Flag>();

            foreach (var question in relevantQuestions)
            {
                var questionKey = question.QuestionNumber.ToString();

                if (!answers.TryGetValue(questionKey, out string response))
                {
                    Console.WriteLine($"No response found for question {question.QuestionNumber} in Booklet Series {bookletSeries}");
                    continue;
                }

                await ProcessQuestionResponseAsync(
                    question, response, barCode, data,
                    ProjectId, WhichDatabase, flagsToCreate);
            }

            // Batch create flags if any
            if (flagsToCreate.Any())
            {
                await CreateFlagsAsync(flagsToCreate, WhichDatabase);
            }
           
        }

        private async Task ProcessQuestionResponseAsync(
            AmbiguousQue question,
            string response,
            string barCode,
            Dictionary<string, string> data,
            int ProjectId,
            string WhichDatabase,
            List<Flag> flagsToCreate
           )
        {
            Console.WriteLine($"Processing Question: {question.QuestionNumber}, Response: {response}, BarCode: {barCode}");

            // Handle multiple responses (marked with "*") for both MarkingId 4 and 5
            if (response == "*")
            {
                var flag = new Flag
                {
                    FlagId = GetNextFlagId(WhichDatabase),
                    Remarks = $"Multiple responses for question {question.QuestionNumber} in Booklet Series {data["Booklet Series"]}",
                    FieldNameValue = "*",
                    Field = "Answers",
                    BarCode = barCode,
                    ProjectId = ProjectId,
                    UpdatedByUserId = 0 // Consider passing current user ID as parameter
                };
                flagsToCreate.Add(flag);
                return;
            }
            else if (question.MarkingId != 4 && question.MarkingId != 5)
            {
                Console.WriteLine($"Unknown MarkingId {question.MarkingId} for question {question.QuestionNumber}");
            }
        }

        private async Task<Dictionary<string, Section>> BuildSectionConfigMapAsync(List<ResponseConfig> responseConfig)
        {
            var sectionConfigMap = new Dictionary<string, Section>();

            foreach (var config in responseConfig)
            {
                try
                {
                    var sections = config.Sections ?? new List<Section>();

                    foreach (var section in sections)
                    {
                        if (!string.IsNullOrEmpty(section.Name))
                        {
                            sectionConfigMap[section.Name] = section;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error deserializing section config: {ex.Message}");
                }
            }

            return sectionConfigMap;
        }

        private async Task CreateFlagsAsync(List<Flag> flags, string whichDatabase)
        {
           
            foreach (var flag in flags)
            {
                bool exists = await _FirstDbcontext.Flags
            .AnyAsync(f => f.BarCode == flag.BarCode &&
                           f.Field == flag.Field &&
                           f.FieldNameValue == flag.FieldNameValue);
                if (!exists)
                {
                    await _FirstDbcontext.Flags.AddAsync(flag);
                    Console.WriteLine($"Flag created: {flag.Remarks} for BarCode: {flag.BarCode}");
                }
                else
                {
                    Console.WriteLine($"Duplicate flag ignored for BarCode: {flag.BarCode}, Question: {flag.FieldNameValue}");
                }
            }
            await _FirstDbcontext.SaveChangesAsync();
        }


        public async Task CheckFieldValuesinRangeAsync(
    List<OMRdata> omrDataList,
    List<FieldConfig> fieldConfigs,
    string fieldName,
    Func<string, string, string, bool> isWithinRange,
    Func<string, List<string>, bool> isPreferredResponse,
    int ProjectId,
    string WhichDatabase)
        {
            foreach (var config in fieldConfigs)
            {
                if (config.FieldName == fieldName)
                {
                    var fieldAttributes = JsonConvert.DeserializeObject<List<FieldAttribute>>(config.FieldAttributesJson);

                    foreach (var attribute in fieldAttributes)
                    {
                        foreach (var omrData in omrDataList)
                        {
                            if (string.IsNullOrEmpty(omrData.OmrData))
                            {
                                continue;
                            }

                            try
                            {
                                var jsonDoc = JsonDocument.Parse(omrData.OmrData);
                                if (jsonDoc.RootElement.TryGetProperty(fieldName, out var fieldElement))
                                {
                                    var fieldValue = fieldElement.GetString();

                                    if (fieldValue.Contains('*'))
                                    {
                                        continue;
                                    }

                                    bool flagAdded = false;

                                    if (isWithinRange != null && !string.IsNullOrEmpty(attribute.MinRange) && !string.IsNullOrEmpty(attribute.MaxRange))
                                    {
                                        if (string.IsNullOrEmpty(fieldValue)) // Check if field value is blank
                                        {
                                            AddFlag(omrData, fieldName, fieldValue, "is Blank", ProjectId, WhichDatabase);
                                            flagAdded = true;
                                        }
                                        else if (!isWithinRange(fieldValue, attribute.MinRange, attribute.MaxRange))
                                        {
                                            AddFlag(omrData, fieldName, fieldValue, "out of range", ProjectId, WhichDatabase);
                                            flagAdded = true;
                                        }
                                    }
                                    else if (isPreferredResponse != null && !string.IsNullOrEmpty(attribute.Responses))
                                    {
                                        var preferredResponses = attribute.Responses.Split(',').ToList();

                                        // Check for blank field value
                                        if (string.IsNullOrEmpty(fieldValue))
                                        {
                                            AddFlag(omrData, fieldName, fieldValue, "is Blank", ProjectId, WhichDatabase);
                                            flagAdded = true;
                                        }
                                        else
                                        {
                                            bool isNumericPreferredResponse = preferredResponses.All(r => int.TryParse(r, out _));
                                            bool isNumericFieldValue = int.TryParse(fieldValue, out int numericFieldValue);

                                            if (isNumericPreferredResponse && isNumericFieldValue)
                                            {
                                                var numericPreferredResponses = preferredResponses.Select(int.Parse).ToList();
                                                if (!numericPreferredResponses.Contains(numericFieldValue))
                                                {
                                                    AddFlag(omrData, fieldName, fieldValue, "not a correct option", ProjectId, WhichDatabase);
                                                    flagAdded = true;
                                                }
                                            }
                                            else if (!isPreferredResponse(fieldValue, preferredResponses))
                                            {
                                                AddFlag(omrData, fieldName, fieldValue, "not a correct option", ProjectId, WhichDatabase);
                                                flagAdded = true;
                                            }
                                        }
                                    }

                                    if (flagAdded && (omrData.Status == 2 || omrData.Status == 3))
                                    {
                                        omrData.Status = 5;
                                    }
                                }
                            }
                            catch (System.Text.Json.JsonException ex)
                            {
                                // Handle JSON parsing errors if needed
                                // Log or record the error if required
                            }
                        }
                    }
                }
            }

            if (WhichDatabase == "Local")
            {
                await _FirstDbcontext.SaveChangesAsync();
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    throw new Exception("Connection Unavailable");
                }
                await _SecondDbContext.SaveChangesAsync();
            }
        }





        public async Task CheckForDuplicateRollNumbersAsync(
List<CorrectedOMRData> correctedOMRDataList,
List<OMRdata> omrDataList,
List<FieldConfig> fieldConfigs,
int ProjectId,
string WhichDatabase,
List<string> absenteeRollNumbers)
        {
            // Helper function to determine if a roll number is within the range specified in field configurations
            bool IsRollNumberOutOfRange(string rollNumber)
            {
                return fieldConfigs.Any(config =>
                {
                    if (config.FieldName == "Roll Number")
                    {
                        var fieldAttributes = JsonConvert.DeserializeObject<List<FieldAttribute>>(config.FieldAttributesJson);
                        return fieldAttributes.Any(attribute => !IsWithinRange(rollNumber, attribute.MinRange, attribute.MaxRange));
                    }
                    return false;
                });
            }

            // Extract roll numbers and barcodes from the OMR data, excluding those with an asterisk and those out of range
            var rollNumberBarcodePairs = omrDataList
                .Where(omrData => !string.IsNullOrEmpty(omrData.OmrData) && omrData.Status == 1)
                .Select(omrData =>
                {
                    try
                    {
                        using var jsonDoc = JsonDocument.Parse(omrData.OmrData);
                        if (jsonDoc.RootElement.TryGetProperty("Roll Number", out var rollElement))
                        {
                            var rollNumber = rollElement.GetString();
                            if (rollNumber != null && !rollNumber.Contains('*') && !IsRollNumberOutOfRange(rollNumber))
                            {
                                return new { RollNumber = rollNumber, Barcode = omrData.BarCode, Source = "OMRData" };
                            }
                        }
                    }
                    catch (System.Text.Json.JsonException ex)
                    {
                        Console.WriteLine($"Error parsing JSON: {ex.Message}");
                    }
                    return null;
                })
                .Where(pair => pair != null)
                .ToList();

            // Extract roll numbers and barcodes from the corrected OMR data
            var correctedRollNumberBarcodePairs = correctedOMRDataList
                .Select(correctedData =>
                {
                    if (!string.IsNullOrEmpty(correctedData.CorrectedOmrData))
                    {
                        try
                        {
                            using var jsonDoc = JsonDocument.Parse(correctedData.CorrectedOmrData);
                            if (jsonDoc.RootElement.TryGetProperty("Roll Number", out var rollElement))
                            {
                                var rollNumber = rollElement.GetString();
                                if (rollNumber != null && !rollNumber.Contains('*') && !IsRollNumberOutOfRange(rollNumber))
                                {
                                    return new { RollNumber = rollNumber, Barcode = correctedData.BarCode, Source = "CorrectedOMRData" };
                                }
                            }
                        }
                        catch (System.Text.Json.JsonException ex)
                        {
                            Console.WriteLine($"Error parsing JSON: {ex.Message}");
                        }
                    }
                    return null;
                })
                .Where(pair => pair != null)
                .ToList();

            // Extract roll numbers from absentee list
            var absenteeRollNumberPairs = absenteeRollNumbers
                .Select(rollNumber => new
                {
                    RollNumber = rollNumber,
                    Barcode = (string)null, // No barcode in absentee list
                    Source = "AbsenteeList"
                })
                .ToList();

            // Combine all roll number pairs
            var allRollNumberBarcodePairs = rollNumberBarcodePairs
                .Concat(correctedRollNumberBarcodePairs)
                .Concat(absenteeRollNumberPairs)
                .ToList();

            // Find duplicates within combined roll numbers
            var duplicateRollNumberGroups = allRollNumberBarcodePairs
                .GroupBy(pair => pair.RollNumber)
                .Where(g => g.Count() > 1)
                .ToList();

            int UserID = 0;
            var userIdClaim = _httpContextAccessor.HttpContext?.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                UserID = userId;
            }

            // Helper function to add a flag
            async Task AddFlagAsync(string rollNumber, string barcode, string source)
            {
                var flag = new Flag
                {
                    FlagId = GetNextFlagId(WhichDatabase),
                    Remarks = $"Duplicate Roll Number found with {source}",
                    FieldNameValue = rollNumber,
                    Field = "Roll Number",
                    BarCode = barcode,
                    ProjectId = ProjectId,
                    UpdatedByUserId = UserID
                };

                if (WhichDatabase == "Local")
                {
                    _FirstDbcontext.Flags.Add(flag);
                    string flagJson = JsonConvert.SerializeObject(flag);
                    _ChangeLogger.LogForDBSync("Insert", "Flags", flagJson, WhichDatabase, UserID);

                    var omrDataToUpdate = _FirstDbcontext.OMRdatas.FirstOrDefault(o => o.BarCode == barcode && o.ProjectId == ProjectId);
                    if (omrDataToUpdate != null && omrDataToUpdate.Status != 5 && omrDataToUpdate.AuditCycleNumber != 0)
                    {
                        omrDataToUpdate.Status = 5;
                    }

                    await _FirstDbcontext.SaveChangesAsync();
                }
                else
                {
                    if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                    {
                        throw new Exception("Connection Unavailable");
                    }

                    _SecondDbContext.Flags.Add(flag);
                    string flagJson = JsonConvert.SerializeObject(flag);
                    _ChangeLogger.LogForDBSync("Insert", "Flags", flagJson, WhichDatabase, UserID);

                    var omrDataToUpdate = _SecondDbContext.OMRdatas.FirstOrDefault(o => o.BarCode == barcode && o.ProjectId == ProjectId);
                    if (omrDataToUpdate != null && omrDataToUpdate.Status != 5 && omrDataToUpdate.AuditCycleNumber != 0)
                    {
                        omrDataToUpdate.Status = 5;
                    }

                    await _SecondDbContext.SaveChangesAsync();
                }

                Console.WriteLine($"Added flag for Roll Number: {rollNumber}, Barcode: {barcode}");
            }
            // Process and flag duplicate roll numbers
            foreach (var group in duplicateRollNumberGroups)
            {
                foreach (var pair in group)
                {
                    await AddFlagAsync(pair.RollNumber, pair.Barcode, pair.Source);
                }
            }

            Console.WriteLine("All flags saved to the database.");
        }




        public async Task CheckFieldContainsCharacterAsync(
     List<OMRdata> omrDataList,
     string fieldName,
     char characterToCheck,
     int ProjectId,
     string WhichDatabase)
        {
            if (string.Equals(fieldName, "Answers", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Skipping processing because fieldName is 'answer'.");
                return;
            }
            Console.WriteLine($"Checking field '{fieldName}' for character '{characterToCheck}' in OMR data...");
            // Extract field values with the specified character from the OMR data
            var fieldValuesWithCharacter = omrDataList
                .Select(omrData =>
                {
                    if (string.IsNullOrEmpty(omrData.OmrData))
                    {
                        return null;
                    }

                    try
                    {
                        using var jsonDoc = JsonDocument.Parse(omrData.OmrData);
                        if (jsonDoc.RootElement.TryGetProperty(fieldName, out var fieldElement))
                        {
                            var fieldValue = fieldElement.GetString();
                            if (fieldValue != null && (fieldValue.Contains(characterToCheck)|| fieldValue.Contains(' ')) )
                            {
                                return new { FieldValue = fieldValue, Barcode = omrData.BarCode };
                            }
                        }
                    }
                    catch (System.Text.Json.JsonException ex)
                    {
                        Console.WriteLine($"Error parsing JSON: {ex.Message}");
                    }

                    return null;
                })
                .Where(pair => pair != null)
                .ToList();
            int UserID = 0;
            var userIdClaim = _httpContextAccessor.HttpContext?.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                UserID = userId;
            }
            foreach (var pair in fieldValuesWithCharacter)
            {
                var flag = new Flag
                {
                    FlagId = GetNextFlagId(WhichDatabase),
                    Remarks = $"{fieldName} contains '{characterToCheck} or space'",
                    FieldNameValue = pair.FieldValue,
                    Field = fieldName,
                    BarCode = pair.Barcode,
                    ProjectId = ProjectId,
                    UpdatedByUserId = UserID
                };

                if (WhichDatabase == "Local")
                {
                    bool alreadyexists = false;
                    var existingflags = _FirstDbcontext.Flags.Where(f => f.BarCode == pair.Barcode && f.ProjectId == ProjectId).ToList();
                    foreach (var existingflag in existingflags)
                    {
                        if (existingflag.Remarks == flag.Remarks)
                        {
                            alreadyexists = true; break;
                        }
                    }
                    if (!alreadyexists)
                    {
                        _FirstDbcontext.Flags.Add(flag);
                        string flagJson = JsonConvert.SerializeObject(flag);
                        _ChangeLogger.LogForDBSync("Insert", "Flags", flagJson, WhichDatabase, UserID);

                        // Update the status of the corresponding OMRdata to 5
                        var omrDataToUpdate = _FirstDbcontext.OMRdatas.FirstOrDefault(o => o.BarCode == pair.Barcode && o.ProjectId == ProjectId);
                        if (omrDataToUpdate != null && omrDataToUpdate.Status != 5 && omrDataToUpdate.AuditCycleNumber != 0)
                        {
                            omrDataToUpdate.Status = 5;
                        }
                    }

                }
                else
                {
                    if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                    {
                        throw new Exception("Connection Unavailable");
                    }
                    bool alreadyexists = false;
                    var existingflags = _SecondDbContext.Flags.Where(f => f.BarCode == pair.Barcode && f.ProjectId == ProjectId).ToList();
                    foreach (var existingflag in existingflags)
                    {
                        if (existingflag.Remarks == flag.Remarks)
                        {
                            alreadyexists = true; break;
                        }
                    }
                    if (!alreadyexists)
                    {
                        _SecondDbContext.Flags.Add(flag);
                        string flagJson = JsonConvert.SerializeObject(flag);
                        _ChangeLogger.LogForDBSync("Insert", "Flags", flagJson, WhichDatabase, UserID);

                        // Update the status of the corresponding OMRdata to 5
                        var omrDataToUpdate = _SecondDbContext.OMRdatas.FirstOrDefault(o => o.BarCode == pair.Barcode && o.ProjectId == ProjectId);
                        if (omrDataToUpdate != null && omrDataToUpdate.Status != 5 && omrDataToUpdate.AuditCycleNumber != 0)
                        {
                            omrDataToUpdate.Status = 5;
                        }
                    }

                }

                Console.WriteLine($"Added flag for {fieldName}: {pair.FieldValue}, Barcode: {pair.Barcode}");
            }

            if (WhichDatabase == "Local")
            {
                await _FirstDbcontext.SaveChangesAsync();
            }
            else
            {
                await _SecondDbContext.SaveChangesAsync();
            }

            Console.WriteLine("All flags saved to the database.");
        }

        

        public async Task CheckWithRegistrationDataAsync(
    List<OMRdata> omrDataList,
    List<RegistrationData> registrationDataList,
    List<CorrectedOMRData> correctedOmrDataList,
    List<FieldConfig> fieldConfigs,
    int projectId,
    string whichDatabase,
    List<string> keysToCheck)
        {
            // Function to parse roll number and check for asterisks and out-of-range values
            bool IsValidRollNumber(string rollNumber, List<FieldConfig> fieldConfigs)
            {
                if (rollNumber.Contains('*')) return false;

                var rollNumberConfig = fieldConfigs.FirstOrDefault(config => config.FieldName == "Roll Number");
                if (rollNumberConfig != null)
                {
                    var fieldAttributes = JsonConvert.DeserializeObject<List<FieldAttribute>>(rollNumberConfig.FieldAttributesJson);
                    foreach (var attribute in fieldAttributes)
                    {
                        if (!IsWithinRange(rollNumber, attribute.MinRange, attribute.MaxRange))
                        {
                            return false;
                        }
                    }
                }
                return true;
            }

            // Function to clean and convert class values
            string CleanClassValue(string classValue)
            {
                return classValue switch
                {
                    "11" => "1",
                    "12" => "2",
                    _ => classValue
                };
            }

            // Function to check if a value is numeric
            bool IsNumeric(string value)
            {
                return int.TryParse(value, out _);
            }

            // Extract valid roll numbers and corresponding JSON objects from OMR data
            var rollNumberBarcodePairs = omrDataList
                .Where(omrData => !string.IsNullOrEmpty(omrData.OmrData))
                .Select(omrData =>
                {
                    try
                    {
                        using var jsonDoc = JsonDocument.Parse(omrData.OmrData);
                        if (jsonDoc.RootElement.TryGetProperty("Roll Number", out var rollElement))
                        {
                            var rollNumber = rollElement.GetString();
                            if (rollNumber != null && IsValidRollNumber(rollNumber, fieldConfigs))
                            {
                                return new { RollNumber = rollNumber, BarCode = omrData.BarCode, OmrData = omrData.OmrData };
                            }
                        }
                    }
                    catch (System.Text.Json.JsonException ex)
                    {
                        Console.WriteLine($"Error parsing JSON: {ex.Message}");
                    }
                    return null;
                })
                .Where(pair => pair != null)
                .ToList();

            // Extract valid roll numbers and corresponding JSON objects from corrected OMR data
            var correctedRollNumberBarcodePairs = correctedOmrDataList
                .Where(correctedData => !string.IsNullOrEmpty(correctedData.CorrectedOmrData))
                .Select(correctedData =>
                {
                    try
                    {
                        using var jsonDoc = JsonDocument.Parse(correctedData.CorrectedOmrData);
                        if (jsonDoc.RootElement.TryGetProperty("Roll Number", out var rollElement))
                        {
                            var rollNumber = rollElement.GetString();
                            if (rollNumber != null && IsValidRollNumber(rollNumber, fieldConfigs))
                            {
                                return new { RollNumber = rollNumber, BarCode = correctedData.BarCode, OmrData = correctedData.CorrectedOmrData };
                            }
                        }
                    }
                    catch (System.Text.Json.JsonException ex)
                    {
                        Console.WriteLine($"Error parsing JSON: {ex.Message}");
                    }
                    return null;
                })
                .Where(pair => pair != null)
                .ToList();

            // Combine both lists of roll number barcode pairs
            var allRollNumberBarcodePairs = rollNumberBarcodePairs.Concat(correctedRollNumberBarcodePairs).ToList();

            // Get field configuration for keys
            var fieldConfigsDictionary = fieldConfigs.ToDictionary(config => config.FieldName, config =>
                JsonConvert.DeserializeObject<List<FieldAttribute>>(config.FieldAttributesJson));

            // Match roll numbers with registration data and check specified keys
            foreach (var pair in allRollNumberBarcodePairs)
            {
                var registrationData = registrationDataList.FirstOrDefault(reg => reg.RollNumber == pair.RollNumber);
                if (registrationData != null)
                {
                    try
                    {
                        // Parse the RegistrationsData JSON string
                        var registrationDataJson = JsonConvert.DeserializeObject<Dictionary<string, string>>(registrationData.RegistrationsData);

                        using var jsonDoc = JsonDocument.Parse(pair.OmrData);
                        foreach (var key in keysToCheck)
                        {
                            if (jsonDoc.RootElement.TryGetProperty(key, out var omrValue))
                            {
                                var omrValueStr = omrValue.GetString();

                                // Fetch the corresponding value from the registration data
                                registrationDataJson.TryGetValue(key, out var registrationValueStr);

                                // Debug logging
                                Console.WriteLine($"Comparing Key: {key}");
                                Console.WriteLine($"OMR Value: {omrValueStr}");
                                Console.WriteLine($"Registration Value: {registrationValueStr}");

                                // Skip if OMR or registration value contains an asterisk (*)
                                if (omrValueStr?.Contains('*') == true || string.IsNullOrEmpty(omrValueStr))
                                {
                                    continue;
                                }

                                // Specific mapping and cleaning for class values
                                if (key == "Class")
                                {
                                    omrValueStr = CleanClassValue(omrValueStr);
                                    registrationValueStr = CleanClassValue(registrationValueStr);
                                }

                                // Fetch field attributes for the current key
                                var fieldAttributes = fieldConfigsDictionary.ContainsKey(key)
                                    ? fieldConfigsDictionary[key]
                                    : new List<FieldAttribute>();

                                bool isOutOfRange = false;
                                bool isPreferred = false;

                                // Check if the value is within range and/or preferred responses
                                foreach (var attribute in fieldAttributes)
                                {
                                    if (!string.IsNullOrEmpty(attribute.MinRange) && !string.IsNullOrEmpty(attribute.MaxRange))
                                    {
                                        if (!IsWithinRange(omrValueStr, attribute.MinRange, attribute.MaxRange))
                                        {
                                            isOutOfRange = true;
                                        }
                                    }
                                    if (!string.IsNullOrEmpty(attribute.Responses))
                                    {
                                        var preferredResponses = attribute.Responses.Split(',').ToList();
                                        if (!IsPreferredResponse(omrValueStr, preferredResponses))
                                        {
                                            isPreferred = false;
                                        }
                                        else
                                        {
                                            isPreferred = true;
                                        }
                                    }
                                }

                                // Skip values that are out of range or not in preferred responses
                                if (isOutOfRange || !isPreferred)
                                {
                                    continue;
                                }

                                // Parse and compare as integers if both values are numeric
                                bool areValuesEqual = false;
                                if (IsNumeric(omrValueStr) && IsNumeric(registrationValueStr))
                                {
                                    areValuesEqual = int.Parse(omrValueStr) == int.Parse(registrationValueStr);
                                }
                                else
                                {
                                    areValuesEqual = omrValueStr == registrationValueStr;
                                }

                                if (!areValuesEqual)
                                {
                                    AddFlag(pair.BarCode, key, omrValueStr, $"Value mismatch with registration data for {key} Value should be {registrationValueStr}", projectId, whichDatabase);
                                }
                            }
                        }
                    }
                    catch (System.Text.Json.JsonException ex)
                    {
                        Console.WriteLine($"Error parsing JSON: {ex.Message}");
                    }
                }
            }

            // Save changes to the database
            if (whichDatabase == "Local")
            {
                await _FirstDbcontext.SaveChangesAsync();
            }
            else
            {
                await _SecondDbContext.SaveChangesAsync();
            }

            Console.WriteLine("All flags saved to the database.");
        }

        public async Task CheckForMismatchedWithExtractedAsync(List<OMRdata> omrDataList, List<ExtractedOMRData> extractedOMRDatas, List<FieldConfig> fieldConfigs, int ProjectId, string WhichDatabase )
        {
            foreach (var corrected in omrDataList)
            {
                var extracted = extractedOMRDatas.FirstOrDefault(e => e.BarCode == corrected.BarCode);
                if (extracted == null)
                {
                    Console.WriteLine($"No extracted data found for BarCode: {corrected.BarCode}");
                    continue;
                }

                var correctedJson = JsonConvert.DeserializeObject<Dictionary<string, object>>(corrected.OmrData);
                var extractedJson = JsonConvert.DeserializeObject<Dictionary<string, object>>(extracted.ExtractedOmrData);

                foreach (var key in correctedJson.Keys)
                {
                    if (!extractedJson.ContainsKey(key))
                    {
                        Console.WriteLine($"Missing key '{key}' in extracted data for BarCode: {corrected.BarCode}");
                        AddFlag(corrected.BarCode,
                                                      key,
                                                     $"Missing {key} in extracted data",
                                                       "Missing in Extracted field",
                                                       ProjectId,
                                                       WhichDatabase);
                        continue;
                    }

                    if (key == "Answers")
                    {
                        var correctedAnswers = JsonConvert.DeserializeObject<Dictionary<string, string>>(correctedJson[key].ToString());
                        var extractedAnswers = JsonConvert.DeserializeObject<Dictionary<string, string>>(extractedJson[key].ToString());

                        foreach (var q in correctedAnswers.Keys)
                        {
                            var correctedAns = correctedAnswers[q];
                            var extractedAns = extractedAnswers.ContainsKey(q) ? extractedAnswers[q] : null;

                            if (correctedAns != extractedAns)
                            {
                                Console.WriteLine($"Mismatch at BarCode: {corrected.BarCode}, Question: {q}, Corrected: '{correctedAns}', Extracted: '{extractedAns}'");
                                AddFlag(
                                                                                    corrected.BarCode,
                                                                                    key,
                                                                                    extractedAns ?? "null",
                                                                                     $"Mismatch in Question: {q} And ScannedAns : {correctedAns}",
                                                                                     ProjectId,
                                                                                     WhichDatabase
                                                                                 );

                            }
                        }
                    }
                    else
                    {
                        var correctedValue = correctedJson[key]?.ToString();
                        var extractedValue = extractedJson[key]?.ToString();

                        if (correctedValue != extractedValue)
                        {
                            Console.WriteLine($"Mismatch in field '{key}' at BarCode: {corrected.BarCode}, Corrected: '{correctedValue}', Extracted: '{extractedValue}'");
                            AddFlag(
                                                      corrected.BarCode,
                                                      key,
                                                      extractedValue ?? "null",
                                                       $"Mismatch in Both {key}, Corrected:{correctedValue}",
                                                       ProjectId,
                                                       WhichDatabase
                                                   );

                        }
                    }
                }
            }
        }
        public async Task CheckForMissingRollNumbersAsync(
    List<OMRdata> omrDataList,
    List<CorrectedOMRData> correctedOMRDatas,
    List<string> absentee,
    List<RegistrationData> registrationDatas,
    List<FieldConfig> fieldConfigs,
    string WhichDatabase, int ProjectId)
        {
            // Helper function to determine if a roll number is within the range specified in field configurations
            bool IsRollNumberOutOfRange(string rollNumber)
            {
                return fieldConfigs.Any(config =>
                {
                    if (config.FieldName == "Roll Number")
                    {
                        var fieldAttributes = JsonConvert.DeserializeObject<List<FieldAttribute>>(config.FieldAttributesJson);
                        return fieldAttributes.Any(attribute => !IsWithinRange(rollNumber, attribute.MinRange, attribute.MaxRange));
                    }
                    return false;
                });
            }

            // Extract roll numbers and barcodes (or Test Booklet Numbers) from OMR data, filtering out those with asterisks and only taking those with status 1
            var rollNumberBarcodePairs = omrDataList
                .Where(omrData => omrData.Status == 1 && omrData.ProjectId == ProjectId)
                .Select(omrData =>
                {
                    if (string.IsNullOrEmpty(omrData.OmrData)) return null;
                    try
                    {
                        using var jsonDoc = JsonDocument.Parse(omrData.OmrData);
                        if (jsonDoc.RootElement.TryGetProperty("Roll Number", out var rollElement))
                        {
                            var rollNumber = rollElement.GetString();
                            string barcode = omrData.BarCode;
                            string testBookletNumber = jsonDoc.RootElement.TryGetProperty("Test Booklet Number", out var tbnElement) ? tbnElement.GetString() : null;

                            if (rollNumber != null && !rollNumber.Contains('*') && !IsRollNumberOutOfRange(rollNumber))
                            {
                                return new { RollNumber = rollNumber, Barcode = barcode, TestBookletNumber = testBookletNumber, Source = "OMRData" };
                            }
                        }
                    }
                    catch (System.Text.Json.JsonException ex)
                    {
                        Console.WriteLine($"Error parsing JSON: {ex.Message}");
                    }
                    return null;
                })
                .Where(pair => pair != null)
                .ToList();

            // Extract roll numbers and barcodes (or Test Booklet Numbers) from Corrected OMR data
            var correctedRollNumberBarcodePairs = correctedOMRDatas
                .Select(correctedData =>
                {
                    if (string.IsNullOrEmpty(correctedData.CorrectedOmrData)) return null;
                    try
                    {
                        using var jsonDoc = JsonDocument.Parse(correctedData.CorrectedOmrData);
                        if (jsonDoc.RootElement.TryGetProperty("Roll Number", out var rollElement))
                        {
                            var rollNumber = rollElement.GetString();
                            string barcode = correctedData.BarCode;
                            string testBookletNumber = jsonDoc.RootElement.TryGetProperty("Test Booklet Number", out var tbnElement) ? tbnElement.GetString() : null;

                            if (rollNumber != null && !rollNumber.Contains('*'))
                            {
                                return new { RollNumber = rollNumber, Barcode = barcode, TestBookletNumber = testBookletNumber, Source = "CorrectedOMRData" };
                            }
                        }
                    }
                    catch (System.Text.Json.JsonException ex)
                    {
                        Console.WriteLine($"Error parsing JSON: {ex.Message}");
                    }
                    return null;
                })
                .Where(pair => pair != null)
                .ToList();

            // Combine roll numbers from OMR data, Corrected OMR data, and absentee list
            var combinedRollNumbers = rollNumberBarcodePairs.Select(pair => pair.RollNumber)
                .Union(correctedRollNumberBarcodePairs.Select(pair => pair.RollNumber))
                .Union(absentee)
                .ToHashSet();

            // Fetch roll numbers from Registration data
            var registrationRollNumbers = new HashSet<string>(registrationDatas.Select(r => r.RollNumber));

            // Identify missing roll numbers
            var missingRollNumbers = registrationRollNumbers.Except(combinedRollNumbers).ToList();

            int UserID = 0;
            var userIdClaim = _httpContextAccessor.HttpContext?.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name); 
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                UserID = userId;
                //_logger.LogEvent($"Deleted BookletPDFData in PaperID: {paperID}", "BookletPdfData", userId);
            }
            // Add each missing roll number to the Flags table
            foreach (var missingRollNumber in missingRollNumbers)
            {
                var pair = rollNumberBarcodePairs.FirstOrDefault(p => p.RollNumber == missingRollNumber)
                           ?? correctedRollNumberBarcodePairs.FirstOrDefault(p => p.RollNumber == missingRollNumber);

                var flag = new Flag
                { 
                    FlagId = GetNextFlagId(WhichDatabase),
                    Remarks = $"Missing Roll Number {missingRollNumber}",
                    FieldNameValue = missingRollNumber,
                    Field = "Roll Number",
                    BarCode = pair?.Barcode ?? pair?.TestBookletNumber, // Use Test Booklet Number if Barcode is not found
                    ProjectId = ProjectId,
                    UpdatedByUserId = UserID
                };

                if (WhichDatabase == "Local")
                {
                    bool alreadyExists = _FirstDbcontext.Flags.Any(f => f.BarCode == flag.BarCode && f.Remarks == flag.Remarks);
                    if (!alreadyExists)
                    {
                        _FirstDbcontext.Flags.Add(flag);
                        string flagJson = JsonConvert.SerializeObject(flag);
                        _ChangeLogger.LogForDBSync("Insert", "Flags", flagJson, WhichDatabase, UserID);
                        await _FirstDbcontext.SaveChangesAsync();
                    }
                }
                else
                {
                    bool alreadyExists = _SecondDbContext.Flags.Any(f => f.BarCode == flag.BarCode && f.Remarks == flag.Remarks);
                    if (!alreadyExists)
                    {
                        _SecondDbContext.Flags.Add(flag);
                        string flagJson = JsonConvert.SerializeObject(flag);
                        _ChangeLogger.LogForDBSync("Insert", "Flags", flagJson, WhichDatabase, UserID);
                        await _SecondDbContext.SaveChangesAsync();
                    }
                }

                Console.WriteLine($"Added flag for Missing Roll Number: {missingRollNumber}, Barcode/Test Booklet Number: {pair?.Barcode ?? pair?.TestBookletNumber}");
            }

            // Identify and flag OMRdata and CorrectedOMRData roll numbers that do not match with any in the registration data list
            var unmatchedOmrRollNumbers = rollNumberBarcodePairs
                .Where(pair => !registrationRollNumbers.Contains(pair.RollNumber))
                .ToList();

            var unmatchedCorrectedOmrRollNumbers = correctedRollNumberBarcodePairs
                .Where(pair => !registrationRollNumbers.Contains(pair.RollNumber))
                .ToList();

            foreach (var unmatchedPair in unmatchedOmrRollNumbers.Concat(unmatchedCorrectedOmrRollNumbers))
            {
                var flag = new Flag
                {
                    FlagId = GetNextFlagId(WhichDatabase),
                    Remarks = $"Unmatched Roll Number {unmatchedPair.RollNumber}",
                    FieldNameValue = unmatchedPair.RollNumber,
                    Field = "Roll Number",
                    BarCode = unmatchedPair.Barcode ?? unmatchedPair.TestBookletNumber, // Use Test Booklet Number if Barcode is not found
                    ProjectId = ProjectId,
                    UpdatedByUserId = UserID
                };

                if (WhichDatabase == "Local")
                {
                    bool alreadyExists = _FirstDbcontext.Flags.Any(f => f.BarCode == flag.BarCode && f.Remarks == flag.Remarks);
                    if (!alreadyExists)
                    {
                        _FirstDbcontext.Flags.Add(flag);
                        string flagJson = JsonConvert.SerializeObject(flag);
                        _ChangeLogger.LogForDBSync("Insert", "Flags", flagJson, WhichDatabase, UserID);
                        await _FirstDbcontext.SaveChangesAsync();
                    }
                }
                else
                {
                    bool alreadyExists = _SecondDbContext.Flags.Any(f => f.BarCode == flag.BarCode && f.Remarks == flag.Remarks);
                    if (!alreadyExists)
                    {
                        _SecondDbContext.Flags.Add(flag);
                        string flagJson = JsonConvert.SerializeObject(flag);
                        _ChangeLogger.LogForDBSync("Insert", "Flags", flagJson, WhichDatabase, UserID);
                        await _SecondDbContext.SaveChangesAsync();
                    }
                }

                Console.WriteLine($"Added flag for Unmatched Roll Number: {unmatchedPair.RollNumber}, Barcode/Test Booklet Number: {unmatchedPair.Barcode ?? unmatchedPair.TestBookletNumber}");
            }

            Console.WriteLine("All flags saved to the database.");
        }





        private void AddFlag(OMRdata omrData, string fieldName, string fieldValue, string remark, int ProjectId, string WhichDatabase)
        {
            int UserID = 0;
            var userIdClaim = _httpContextAccessor.HttpContext?.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                UserID = userId;
            }

            var flag = new Flag
            {
                FlagId = GetNextFlagId(WhichDatabase),
                Remarks = $"{fieldName} {remark}",
                FieldNameValue = fieldValue,
                BarCode = omrData.BarCode,
                ProjectId = ProjectId, // Or any relevant project ID
                Field = fieldName,
                UpdatedByUserId = UserID
            };

            if (WhichDatabase == "Local")
            {
                bool alreadyexists = false;
                var existingflags = _FirstDbcontext.Flags.Where(f => f.BarCode == omrData.BarCode && f.ProjectId == ProjectId).ToList();
                foreach (var existingflag in existingflags)
                {
                    if (existingflag.Remarks == flag.Remarks)
                    {
                        alreadyexists = true; break;
                    }
                }
                if (!alreadyexists)
                {
                    _FirstDbcontext.Flags.Add(flag);
                    string flagJson = JsonConvert.SerializeObject(flag);
                    _ChangeLogger.LogForDBSync("Insert", "Flags", flagJson, WhichDatabase, UserID);
                }

            }

            else
            {
                if (!_connectionChecker.IsOnlineDatabaseAvailable())
                {
                    throw new Exception("Connection Unavailable");
                }
                bool alreadyexists = false;
                var existingflags = _SecondDbContext.Flags.Where(f => f.BarCode == omrData.BarCode && f.ProjectId == ProjectId).ToList();
                foreach (var existingflag in existingflags)
                {
                    if (existingflag.Remarks == flag.Remarks)
                    {
                        alreadyexists = true; break;
                    }
                }
                if (!alreadyexists)
                {
                    _SecondDbContext.Flags.Add(flag);
                    string flagJson = JsonConvert.SerializeObject(flag);
                    _ChangeLogger.LogForDBSync("Insert", "Flags", flagJson, WhichDatabase, UserID);
                }

            }

        }

        private void AddFlag(string barCode, string field, string fieldValue, string remarks, int projectId, string whichDatabase)
        {
            int UserID = 0;
            var userIdClaim = _httpContextAccessor.HttpContext?.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                UserID = userId;
                //_logger.LogEvent($"Deleted BookletPDFData in PaperID: {paperID}", "BookletPdfData", userId);
            }


            var flag = new Flag
            {
                FlagId = GetNextFlagId(whichDatabase),
                Remarks = remarks,
                FieldNameValue = fieldValue,
                Field = field,
                BarCode = barCode,
                ProjectId = projectId
            };

            if (whichDatabase == "Local")
            {
                bool alreadyexists = false;
                var existingflags = _FirstDbcontext.Flags.Where(f => f.BarCode == barCode && f.ProjectId == projectId).ToList();
                foreach (var existingflag in existingflags)
                {
                    if (existingflag.Remarks == flag.Remarks)
                    {
                        alreadyexists = true; break;
                    }
                }
                if (!alreadyexists)
                {
                    _FirstDbcontext.Flags.Add(flag);
                    string flagJson = JsonConvert.SerializeObject(flag);
                    _ChangeLogger.LogForDBSync("Insert", "Flags", flagJson, whichDatabase, UserID);
                }

            }

            else
            {
                if (!_connectionChecker.IsOnlineDatabaseAvailable())
                {
                    throw new Exception("Connection Unavailable");
                }
                bool alreadyexists = false;
                var existingflags = _SecondDbContext.Flags.Where(f => f.BarCode == barCode && f.ProjectId == projectId).ToList();
                foreach (var existingflag in existingflags)
                {
                    if (existingflag.Remarks == flag.Remarks)
                    {
                        alreadyexists = true; break;
                    }
                }
                if (!alreadyexists)
                {
                    _SecondDbContext.Flags.Add(flag);
                    string flagJson = JsonConvert.SerializeObject(flag);
                    _ChangeLogger.LogForDBSync("Insert", "Flags", flagJson, whichDatabase, UserID);
                }

            }

            Console.WriteLine($"Added flag for BarCode: {barCode}, Field: {field}, Remarks: {remarks}");
        }
        // Example of a method to check if a value is within range

        public bool IsPreferredResponse(string value, List<string> preferredResponses)
        {
            return preferredResponses.Contains(value, StringComparer.OrdinalIgnoreCase);
        }


        private bool IsWithinRange(string value, string minRange, string maxRange)
        {
            if (string.IsNullOrEmpty(minRange) && string.IsNullOrEmpty(maxRange))
            {
                return true;
            }

            if (int.TryParse(value, out int intValue))
            {
                if (!string.IsNullOrEmpty(minRange) && intValue < int.Parse(minRange))
                {
                    return false;
                }

                if (!string.IsNullOrEmpty(maxRange) && intValue > int.Parse(maxRange))
                {
                    return false;
                }
            }

            return true;
        }

        private int GetNextFlagId(string WhichDatabase)
        {
            return WhichDatabase == "Local" ? _FirstDbcontext.Flags.Max(c => (int?)c.FlagId) + 1 ?? 1 : _SecondDbContext.Flags.Max(c => (int?)c.FlagId) + 1 ?? 1;
        }

    }

}
