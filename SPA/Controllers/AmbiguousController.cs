﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SPA.Data;
using SPA.Models;

namespace SPA.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AmbiguityController : ControllerBase
    {
        private readonly FirstDbContext _firstDbContext;
        private readonly SecondDbContext _secondDbContext;

        public AmbiguityController(FirstDbContext firstDbContext, SecondDbContext secondDbContext)
        {
            _firstDbContext = firstDbContext;
            _secondDbContext = secondDbContext;
        }
        [HttpGet]
        public ActionResult<IEnumerable<AmbiguousQue>> GetAmbiguousQue()
        {
            return _firstDbContext.AmbiguousQues.ToList();
        }
        [AllowAnonymous]
        [HttpGet("ByProjectId/{projectId:int}")]
        public ActionResult<IEnumerable<AmbiguousQue>> GetAmbiguousQuebyProjectID(int projectId)
        {
            var ambiguousQuestions = _firstDbContext.AmbiguousQues.Where(u => u.ProjectId == projectId).ToList();
            if (ambiguousQuestions == null)
                return NotFound();
            return Ok(ambiguousQuestions);
        }

        [AllowAnonymous]
        [HttpGet("ContainsMarkingRule/{projectId:int}")]
        public ActionResult<bool> ContainsMarkingRule(int projectId)
        {
            var containsMarkingRule = _firstDbContext.AmbiguousQues.Where(mr => mr.ProjectId == projectId && (mr.MarkingId == 5 || mr.MarkingId == 4))
                .Select(a=>a.CourseName).Distinct().ToList();
            return Ok(containsMarkingRule);
        }

        [HttpGet("BSetResponsesByProject/{projectId}")]
        public async Task<IActionResult> GetBSetResponsesByProject(int projectId)
        {
            var fieldConfig = await _firstDbContext.FieldConfigs
                .Where(fc => fc.ProjectId == projectId && fc.FieldName == "Booklet Set")
                .FirstOrDefaultAsync();

            if (fieldConfig == null)
            {
                return NotFound("No BSet found for the given project ID.");
            }

            return Ok(fieldConfig.FieldAttributes.FirstOrDefault()?.Responses);
        }
        
        [AllowAnonymous]
        [HttpPost("allot-marks")]
        public async Task<IActionResult> AllotMarks(string WhichDatabase, [FromBody] List<AmbiguousQue> requests)
        {
            if (requests == null || !requests.Any())
            {
                return BadRequest(new { message = "Invalid request data" });
            }
        
            // Determine the database context based on WhichDatabase parameter
            DbContext dbContext = WhichDatabase == "Local" ? (DbContext)_firstDbContext : (DbContext)_secondDbContext;
        
            // Get the ProjectId from the first request (assuming all requests have the same ProjectId)
            var projectId = requests.First().ProjectId;
            var courseName = requests.First().CourseName;
        
            // Delete existing entries for the same ProjectId
            var existingEntries = dbContext.Set<AmbiguousQue>().Where(q => q.ProjectId == projectId && q.CourseName == courseName);
            dbContext.Set<AmbiguousQue>().RemoveRange(existingEntries);
        
            // Process each request in the array
            foreach (var request in requests)
            {
                Console.WriteLine(request.Option);
                // Create a new record
                var ambiguousQuestion = new AmbiguousQue
                {
                    ProjectId = request.ProjectId,
                    MarkingId = request.MarkingId,
                    SetCode = request.SetCode,
                    QuestionNumber = request.QuestionNumber,
                    Option = request.Option,
                    CourseName = request.CourseName,
                    Section = request.Section
                };
        
                dbContext.Set<AmbiguousQue>().Add(ambiguousQuestion);
            }
        
            // Save changes to the selected database
            await dbContext.SaveChangesAsync();
        
            return Ok(new { message = "Marks allotted successfully" });
        }




        [AllowAnonymous]
        [HttpGet("MarkingRule")]
        public ActionResult<IEnumerable<MarkingRule>> GetMarkingRules()
        {
            return _firstDbContext.MarkingRules.ToList();
        }


        [AllowAnonymous]
        [HttpDelete]
        public async Task<IActionResult> DeleteAmbiguosQuestions(string WhichDatabase, int ProjectId)
        {
            List<AmbiguousQue> ambiguousQues = new List<AmbiguousQue>();

            if (WhichDatabase == "Local")
            {
                ambiguousQues = _firstDbContext.AmbiguousQues.Where(u => u.ProjectId == ProjectId).ToList();
                _firstDbContext.AmbiguousQues.RemoveRange(ambiguousQues);
                await _firstDbContext.SaveChangesAsync();
            }
            else
            {
                ambiguousQues = _secondDbContext.AmbiguousQues.Where(u => u.ProjectId == ProjectId).ToList();
                _secondDbContext.AmbiguousQues.RemoveRange(ambiguousQues);
                await _secondDbContext.SaveChangesAsync();
            }

            return Ok(new { message = "Deleted Successfully" });
        }

    }
}
