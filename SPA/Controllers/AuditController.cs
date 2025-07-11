using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SPA.Data;
using System.Text.Json;
using SPA.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace SPA.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AuditController : ControllerBase
    {

        private readonly AuditService _auditService;
        private readonly ILoggerService _logger;
        private readonly FirstDbContext _context;

        public AuditController(AuditService auditService, ILoggerService logger, FirstDbContext context)
        {
            _auditService = auditService;
            _logger = logger;
            _context = context;
        }

        [HttpGet("RangeAudit")]
        public async Task<IActionResult> RangeAudit(string WhichDatabase, int ProjectId)
        {
            try
            {
                await _auditService.PerformRangeAuditAsync(WhichDatabase, ProjectId);
                var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    _logger.LogEvent($"RangeAudit Ran for ProjectId : {ProjectId}", "Audit", userId, WhichDatabase);
                }
                return Ok("Audit ran Successfully");

            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString(), ex.Message, "Audit", WhichDatabase);
                return BadRequest(ex.Message);
            }

        }

        [HttpGet]
        public async Task<IActionResult> GetAudits(string WhichDatabase, int ProjectId)
        {
            try
            {
                string searchPattern = $"%Ran for ProjectId : {ProjectId}%";

                var audits = await _context.EventLogs
                    .Where(log => EF.Functions.Like(log.Event, searchPattern))
                    .Select(l=>l.Event)
                    .ToListAsync();

                return Ok(audits);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString(), ex.Message, "Audit", WhichDatabase);
                return BadRequest(ex.Message);
            }
        }



        [HttpGet("RegistrationAudit")]
        public async Task<IActionResult> RegistrationAudit(string WhichDatabase, int ProjectId)
        {
            try
            {
                await _auditService.PerformCheckwithRegistrationAuditAsync(WhichDatabase, ProjectId);
                var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    _logger.LogEvent($"RegistrationAudit Ran for ProjectId : {ProjectId}", "Audit", userId, WhichDatabase);
                }
                return Ok("Audit ran Successfully");

            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString(), ex.Message, "Audit", WhichDatabase);
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("DuplicateRollNumberAudit")]
        public async Task<IActionResult> DuplicateRollNumberAudit(string WhichDatabase, int ProjectId)
        {
            try
            {
                await _auditService.PerformDuplcateAuditAsync(WhichDatabase, ProjectId);
                var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    _logger.LogEvent($"DuplicateRollNumberAudit Ran for ProjectId : {ProjectId}", "Audit", userId, WhichDatabase);
                }
                return Ok("Audit ran Successfully");

            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString(), ex.Message, "Audit", WhichDatabase);
                return BadRequest(ex.Message);
            }


        }

        [HttpGet("ContainsCharacterAudit")]
        public async Task<IActionResult> ContainsCharacterAudit(string WhichDatabase, int ProjectId)
        {
            try
            {
                await _auditService.PerformContainsCharacterAuditAsync(WhichDatabase, ProjectId);
                var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    _logger.LogEvent($"ContainsCharacterAudit Ran for ProjectId : {ProjectId}", "Audit", userId, WhichDatabase);
                }
                return Ok("Audit ran Successfully");

            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString(), ex.Message, "Audit", WhichDatabase);
                return BadRequest(ex.Message);
            }


        }

        [HttpGet("MissingRollNumbers")]
        public async Task<IActionResult> AuditMissingRollNumbers(string WhichDatabase, int ProjectId)
        {
            try
            {
                await _auditService.PerformMissingRollNumberAuditAsync(WhichDatabase, ProjectId);
                var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    _logger.LogEvent($"MissingRollNumbers Ran for ProjectId : {ProjectId}", "Audit", userId, WhichDatabase);
                }
                return Ok("Audit ran Successfully");

            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString(), ex.Message, "Audit", WhichDatabase);
                return BadRequest(ex.Message);
            }
        }

        [AllowAnonymous]
        [HttpGet("MismatchedWithExtracted")]
        public async Task<IActionResult> AuditMismatchedWithExtracted(string WhichDatabase, int ProjectId)
        {
            try
            {
                await _auditService.PerformMismatchedWithExtractedAsync(WhichDatabase, ProjectId);
                var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    _logger.LogEvent($"MismatchedWithExtracted Ran for ProjectId : {ProjectId}", "Audit", userId, WhichDatabase);
                }
                return Ok("Audit ran Successfully");

            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString(), ex.Message, "Audit", WhichDatabase);
                return BadRequest(ex.Message);
            }
        }

        [AllowAnonymous]
        [HttpGet("MultipleResponses")]
        public async Task<IActionResult> AuditMultipleResponses(string WhichDatabase, int ProjectId, string CourseName)
        {
            try
            {
                await _auditService.PerformMultipleResponsesAuditAsync(WhichDatabase, ProjectId, CourseName);
                var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    _logger.LogEvent($"MultipleResponses Ran for ProjectId : {ProjectId}", "Audit", userId, WhichDatabase);
                }
                return Ok("Audit ran Successfully");

            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString(), ex.Message, "Audit", WhichDatabase);
                return BadRequest(ex.Message);
            }
        }

    }
}