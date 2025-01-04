using Microsoft.AspNetCore.Authorization;
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
        /*[AllowAnonymous]
        [HttpPost("allot-marks")]
        public async Task<IActionResult> AllotMarks(string WhichDatabase, [FromBody] AmbiguousQue request)
        {
            if (request == null)
            {
                return BadRequest(new { message = "Invalid request data" });
            }

            // Determine the database context based on WhichDatabase parameter
            AmbiguousQue existingQuestion;
            if (WhichDatabase == "Local")
            {
                existingQuestion = await _firstDbContext.AmbiguousQues
                    .FirstOrDefaultAsync(q => q.ProjectId == request.ProjectId
                                              && q.SetCode == request.SetCode);
            }
            else
            {
                existingQuestion = await _secondDbContext.AmbiguousQues
                    .FirstOrDefaultAsync(q => q.ProjectId == request.ProjectId
                                              && q.SetCode == request.SetCode);
            }

            if (existingQuestion != null)
            {
                // Update existing record
                existingQuestion.MarkingId = request.MarkingId;
                existingQuestion.Option = request.Option;
                existingQuestion.QuestionNumber = request.QuestionNumber;

                if (WhichDatabase == "Local")
                {
                    _firstDbContext.AmbiguousQues.Update(existingQuestion);
                }
                else
                {
                    _secondDbContext.AmbiguousQues.Update(existingQuestion);
                }
            }
            else
            {
                // Create a new record
                var ambiguousQuestion = new AmbiguousQue
                {
                    ProjectId = request.ProjectId,
                    MarkingId = request.MarkingId,
                    SetCode = request.SetCode,
                    QuestionNumber = request.QuestionNumber,
                    Option = request.Option,
                };

                if (WhichDatabase == "Local")
                {
                    _firstDbContext.AmbiguousQues.Add(ambiguousQuestion);
                }
                else
                {
                    _secondDbContext.AmbiguousQues.Add(ambiguousQuestion);
                }
            }

            // Save changes to the selected database
            if (WhichDatabase == "Local")
            {
                await _firstDbContext.SaveChangesAsync();
            }
            else
            {
                await _secondDbContext.SaveChangesAsync();
            }

            return Ok(new { message = "Marks allotted successfully" });
        }*/

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

            // Delete existing entries for the same ProjectId
            var existingEntries = dbContext.Set<AmbiguousQue>().Where(q => q.ProjectId == projectId);
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
