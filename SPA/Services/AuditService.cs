﻿using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SPA.Data;
using System.Text.Json;

namespace SPA.Services
{
    public class AuditService
    {
        private readonly OmrDataService _omrDataService;
        private readonly FieldConfigService _fieldConfigService;
        private readonly RegistrationDataService _registrationDataService;
        private readonly FirstDbContext _firstDbContext;
        private readonly SecondDbContext _secondDbContext;
        private readonly DatabaseConnectionChecker _connectionChecker;

        public AuditService(OmrDataService omrDataService, FieldConfigService fieldConfigService, RegistrationDataService registrationDataService, FirstDbContext firstDbContext, SecondDbContext secondDbContext, DatabaseConnectionChecker connectionChecker)
        {
            _omrDataService = omrDataService;
            _fieldConfigService = fieldConfigService;
            _registrationDataService = registrationDataService;
            _firstDbContext = firstDbContext;
            _secondDbContext = secondDbContext;
            _connectionChecker = connectionChecker;
        }

        public async Task PerformRangeAuditAsync(string WhichDatabase, int ProjectId)
        {
            try
            {
                var correctedomrDataList = await _omrDataService.GetCorrectedOmrDataListAsync(WhichDatabase, ProjectId);
                var omrDataList = await _omrDataService.GetOmrDataListAsync(WhichDatabase, ProjectId);
                var firstindex = omrDataList.First().OmrDataId;
                var fieldConfigs = await _fieldConfigService.GetFieldConfigsAsync(WhichDatabase, ProjectId);
                var registrationDataList = await _registrationDataService.GetRegistrationDataListAsync(WhichDatabase, ProjectId);
                var absenteelist = await _omrDataService.GetAbsenteeListAsync(WhichDatabase, ProjectId);
                var existingFlags = WhichDatabase == "Local"
                            ? _firstDbContext.Flags.Where(f => f.ProjectId == ProjectId && f.Remarks.Contains("IsBlank") && f.isCorrected == false).ToList()
                            : _secondDbContext.Flags.Where(f => f.ProjectId == ProjectId && f.Remarks.Contains("IsBlank") && f.isCorrected == false).ToList();

                foreach (var config in fieldConfigs)
                {
                    await _fieldConfigService.CheckFieldValuesinRangeAsync(omrDataList, fieldConfigs, config.FieldName, IsWithinRange, IsPreferredResponse, ProjectId, WhichDatabase);
                }
                foreach (var omrdata in omrDataList)
                {
                    omrdata.AuditCycleNumber++;
                }
                if (WhichDatabase == "Local")
                {
                    await _firstDbContext.SaveChangesAsync();
                }
                else
                {
                    await _secondDbContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred while performing the audit." + ex.Message);
            }

        }

        /*  public async Task PerformMismatchedWithExtractedAsync(string WhichDatabase, int ProjectId)
          {
              try
              {
                  var correctedOmrDataList = await _omrDataService.GetOmrDataListAsync(WhichDatabase, ProjectId);
                  var extractedOmrDataList = await _omrDataService.GetExtractedOmrDataListAsync(WhichDatabase, ProjectId);
                  var fieldConfigs = await _fieldConfigService.GetFieldConfigsAsync(WhichDatabase, ProjectId);
                  Console.WriteLine($"Corrected OMR Data Count: {extractedOmrDataList.Count}");

                  foreach (var corrected in correctedOmrDataList)
                  {
                      var extracted = extractedOmrDataList.FirstOrDefault(e => e.BarCode == corrected.BarCode);
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
                              continue;
                          }

                          // If it's the "Answers" field, do deep comparison
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
                              }
                          }
                      }
                  }
              }
              catch (Exception ex)
              {
                  Console.WriteLine("An error occurred while performing the audit: " + ex.Message);
              }
          }*/

        public async Task PerformMismatchedWithExtractedAsync(string whichDatabase, int projectId)
        {
            try
            {
                var correctedOmrDataList = await _omrDataService.GetOmrDataListAsync(whichDatabase, projectId);
                var extractedOmrDataList = await _omrDataService.GetExtractedOmrDataListAsync(whichDatabase, projectId);
                var fieldConfigs = await _fieldConfigService.GetFieldConfigsAsync(whichDatabase, projectId);
                Console.WriteLine($"Corrected OMR Data Count: {extractedOmrDataList.Count}");
                if(extractedOmrDataList!=null && extractedOmrDataList.Count > 0) { 

                 await _fieldConfigService.CheckForMismatchedWithExtractedAsync(correctedOmrDataList, extractedOmrDataList, fieldConfigs, projectId, whichDatabase);
                }
              
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred while performing the audit: " + ex.Message);
            }
        }

        public async Task PerformMultipleResponsesAuditAsync(string WhichDatabase, int ProjectId, string CourseName)
        {
            try
            {
                var correctedomrDataList = await _omrDataService.GetCorrectedOmrDataListAsync(WhichDatabase, ProjectId);
                var omrDataList = await _omrDataService.GetOmrDataListWithStatus1Async(WhichDatabase, ProjectId);
                var fieldConfigs = await _fieldConfigService.GetFieldConfigsAsync(WhichDatabase, ProjectId);
                var ambiguousQueList = await _omrDataService.GetAmbiguousQuesAsync(WhichDatabase, ProjectId, CourseName);
                foreach (var config in fieldConfigs)
                {
                    await _fieldConfigService.CheckForMultipleResponsesAsync(omrDataList,correctedomrDataList, ambiguousQueList, config.FieldName, ProjectId, WhichDatabase);
                }
                foreach (var omrdata in omrDataList)
                {
                    omrdata.AuditCycleNumber++;
                }
                if (WhichDatabase == "Local")
                {
                    await _firstDbContext.SaveChangesAsync();
                }
                else
                {
                    await _secondDbContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred while performing the audit." + ex.Message);
            }
        }

        public async Task PerformCheckwithRegistrationAuditAsync(string WhichDatabase, int ProjectId)
        {
            try
            {
                var correctedomrDataList = await _omrDataService.GetCorrectedOmrDataListAsync(WhichDatabase, ProjectId);
                var omrDataList = await _omrDataService.GetOmrDataListAsync(WhichDatabase, ProjectId);
                var firstindex = omrDataList.First().OmrDataId;
                var fieldConfigs = await _fieldConfigService.GetFieldConfigsAsync(WhichDatabase, ProjectId);
                var registrationDataList = await _registrationDataService.GetRegistrationDataListAsync(WhichDatabase, ProjectId);
                var absenteelist = await _omrDataService.GetAbsenteeListAsync(WhichDatabase, ProjectId);

                if (registrationDataList != null)
                {
                    var keysToCheck = await GetCommonKeysAsync(WhichDatabase, ProjectId, firstindex);
                    await _fieldConfigService.CheckWithRegistrationDataAsync(omrDataList, registrationDataList, correctedomrDataList, fieldConfigs, ProjectId, WhichDatabase, keysToCheck);

                }


                foreach (var omrdata in omrDataList)
                {
                    omrdata.AuditCycleNumber++;
                }
                if (WhichDatabase == "Local")
                {
                    await _firstDbContext.SaveChangesAsync();
                }
                else
                {
                    await _secondDbContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred while performing the audit." + ex.Message);
            }

        }

        public async Task PerformContainsCharacterAuditAsync(string WhichDatabase, int ProjectId)
        {
            try
            {
                var correctedomrDataList = await _omrDataService.GetCorrectedOmrDataListAsync(WhichDatabase, ProjectId);
                var omrDataList = await _omrDataService.GetOmrDataListAsync(WhichDatabase, ProjectId);
                var firstindex = omrDataList.First().OmrDataId;
                var fieldConfigs = await _fieldConfigService.GetFieldConfigsAsync(WhichDatabase, ProjectId);
                var registrationDataList = await _registrationDataService.GetRegistrationDataListAsync(WhichDatabase, ProjectId);
                var absenteelist = await _omrDataService.GetAbsenteeListAsync(WhichDatabase, ProjectId);

                foreach (var config in fieldConfigs)
                {
                    await _fieldConfigService.CheckFieldContainsCharacterAsync(omrDataList, config.FieldName, '*', ProjectId, WhichDatabase);

                }
                foreach (var omrdata in omrDataList)
                {
                    omrdata.AuditCycleNumber++;
                }
                if (WhichDatabase == "Local")
                {
                    await _firstDbContext.SaveChangesAsync();
                }
                else
                {
                    await _secondDbContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred while performing the audit." + ex.Message);
            }

        }

        public async Task PerformDuplcateAuditAsync(string WhichDatabase, int ProjectId)
        {
            try
            {
                var correctedomrDataList = await _omrDataService.GetCorrectedOmrDataListAsync(WhichDatabase, ProjectId);
                var omrDataList = await _omrDataService.GetOmrDataListAsync(WhichDatabase, ProjectId);
                var firstindex = omrDataList.First().OmrDataId;
                var fieldConfigs = await _fieldConfigService.GetFieldConfigsAsync(WhichDatabase, ProjectId);
                var registrationDataList = await _registrationDataService.GetRegistrationDataListAsync(WhichDatabase, ProjectId);
                var absenteelist = await _omrDataService.GetAbsenteeListAsync(WhichDatabase, ProjectId);


                await _fieldConfigService.CheckForDuplicateRollNumbersAsync(correctedomrDataList, omrDataList, fieldConfigs, ProjectId, WhichDatabase, absenteelist);

                foreach (var omrdata in omrDataList)
                {
                    omrdata.AuditCycleNumber++;
                }
                if (WhichDatabase == "Local")
                {
                    await _firstDbContext.SaveChangesAsync();
                }
                else
                {
                    await _secondDbContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred while performing the audit." + ex.Message);
            }

        }

        public async Task PerformMissingRollNumberAuditAsync(string WhichDatabase, int ProjectId)
        {
            try
            {
                var correctedomrDataList = await _omrDataService.GetCorrectedOmrDataListAsync(WhichDatabase, ProjectId);
                var omrDataList = await _omrDataService.GetOmrDataListAsync(WhichDatabase, ProjectId);
                var fieldConfigs = await _fieldConfigService.GetFieldConfigsAsync(WhichDatabase, ProjectId);
                var registrationDataList = await _registrationDataService.GetRegistrationDataListAsync(WhichDatabase, ProjectId);
                var absenteelist = await _omrDataService.GetAbsenteeListAsync(WhichDatabase, ProjectId);

                await _fieldConfigService.CheckForMissingRollNumbersAsync(omrDataList, correctedomrDataList, absenteelist, registrationDataList, fieldConfigs, WhichDatabase, ProjectId);


                foreach (var omrdata in omrDataList)
                {
                    omrdata.AuditCycleNumber++;
                }
                if (WhichDatabase == "Local")
                {
                    await _firstDbContext.SaveChangesAsync();
                }
                else
                {
                    await _secondDbContext.SaveChangesAsync();
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }



        }

     

        public async Task<List<string>> GetCommonKeysAsync(string whichDatabase, int projectId, int omrDataId)
        {
            IQueryable<RegistrationData> registrationQuery = whichDatabase == "Local" ? _firstDbContext.RegistrationDatas : _secondDbContext.RegistrationDatas;
            IQueryable<OMRdata> omrQuery = whichDatabase == "Local" ? _firstDbContext.OMRdatas : _secondDbContext.OMRdatas;

            // Fetch the relevant registration data record
            var registrationData = await registrationQuery.FirstOrDefaultAsync(u => u.ProjectId == projectId);
            if (registrationData == null)
            {
                throw new Exception("No registration data found.");
            }

            // Fetch the relevant OMR data record
            var omrData = await omrQuery.FirstOrDefaultAsync(u => u.ProjectId == projectId && u.OmrDataId == omrDataId);
            if (omrData == null)
            {
                throw new Exception("No OMR data found.");
            }

            // Parse the JSON data to extract keys
            var registrationJsonData = registrationData.RegistrationsData;
            var omrJsonData = omrData.OmrData;

            var registrationKeys = new HashSet<string>();
            var omrKeys = new HashSet<string>();

            using (JsonDocument doc = JsonDocument.Parse(registrationJsonData))
            {
                foreach (JsonProperty element in doc.RootElement.EnumerateObject())
                {
                    registrationKeys.Add(element.Name);
                }
            }

            using (JsonDocument doc = JsonDocument.Parse(omrJsonData))
            {
                foreach (JsonProperty element in doc.RootElement.EnumerateObject())
                {
                    omrKeys.Add(element.Name);
                }
            }

            // Find common keys
            var commonKeys = registrationKeys.Intersect(omrKeys).ToList();

            return commonKeys;
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

        public bool IsPreferredResponse(string value, List<string> preferredResponses)
        {
            return preferredResponses.Contains(value, StringComparer.OrdinalIgnoreCase);
        }




    }

}
