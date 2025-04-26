using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.InkML;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SPA;
using SPA.Data;
using SPA.Services;

namespace SPA.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class FlagsController : ControllerBase
    {
        private readonly FirstDbContext _firstDbContext;
        private readonly SecondDbContext _secondDbContext;
        private readonly IChangeLogger _changeLogger;
        private readonly DatabaseConnectionChecker _connectionChecker;
        private readonly ILoggerService _logger;

        public FlagsController(FirstDbContext context, SecondDbContext secondDbContext, IChangeLogger changeLogger, DatabaseConnectionChecker connectionChecker, ILoggerService logger)
        {
            _firstDbContext = context;
            _secondDbContext = secondDbContext;
            _changeLogger = changeLogger;
            _connectionChecker = connectionChecker;
            _logger = logger;
        }

      

        // GET: api/Flags/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Flag>> GetFlag(int id)
        {
            var flag = await _firstDbContext.Flags.FindAsync(id);

            if (flag == null)
            {
                return NotFound();
            }

            return flag;
        }

        [HttpGet("counts/projectId")]
        public async Task<ActionResult<object>> GetCountsByProjectId(int projectId, string WhichDatabase)
        {
            if (WhichDatabase == "Local")
            {
                try
                {
                    var countsByFieldname = await _firstDbContext.Flags
                        .Where(f => f.ProjectId == projectId && !f.isCorrected)
                        .GroupBy(f => f.Field)
                        .Select(g => new { FieldName = g.Key, Count = g.Count() })
                        .ToListAsync();

                    var remarksCounts = await _firstDbContext.Flags
                        .Where(f => f.ProjectId == projectId && !string.IsNullOrEmpty(f.Remarks) && !f.isCorrected)
                        .GroupBy(f => f.Remarks)
                        .Select(g => new { Remark = g.Key, Count = g.Count() })
                        .ToListAsync();

                    var totalCount = await _firstDbContext.Flags
                        .Where(f => f.ProjectId == projectId)
                        .CountAsync();

                    var corrected = await _firstDbContext.Flags
                        .Where(f => f.ProjectId == projectId && f.isCorrected)
                        .CountAsync();

                    var remaining = totalCount - corrected;

                    var result = new
                    {
                        TotalCount = totalCount,
                        CountsByFieldname = countsByFieldname,
                        RemarksCounts = remarksCounts,
                        Corrected = corrected,
                        Remaining = remaining
                    };

                    return Ok(result);
                }
                catch (Exception ex)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, $"Error retrieving counts: {ex.Message}");
                }
            }
            else
            {

                try
                {
                    if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                    {
                        return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                    }
                    var countsByFieldname = await _secondDbContext.Flags
                        .Where(f => f.ProjectId == projectId && !f.isCorrected)
                        .GroupBy(f => f.Field)
                        .Select(g => new { FieldName = g.Key, Count = g.Count() })
                    .ToListAsync();

                    var remarksCounts = await _secondDbContext.Flags
                        .Where(f => f.ProjectId == projectId && !string.IsNullOrEmpty(f.Remarks) && !f.isCorrected)
                        .GroupBy(f => f.Remarks)
                        .Select(g => new { Remark = g.Key, Count = g.Count() })
                    .ToListAsync();

                    var totalCount = await _secondDbContext.Flags
                        .Where(f => f.ProjectId == projectId)
                    .CountAsync();

                    var corrected = await _secondDbContext.Flags
                        .Where(f => f.ProjectId == projectId && f.isCorrected)
                        .CountAsync();

                    var remaining = totalCount - corrected;

                    var result = new
                    {
                        TotalCount = totalCount,
                        CountsByFieldname = countsByFieldname,
                        RemarksCounts = remarksCounts,
                        Corrected = corrected,
                        Remaining = remaining
                    };

                    return Ok(result);
                }
                catch (Exception ex)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, $"Error retrieving counts: {ex.Message}");
                }
            }
        }

        [HttpGet("ByProject/{ProjectId}")]
        public async Task<ActionResult<List<Flag>>> GetFlagbyProject(int ProjectId, string WhichDatabase)
        {

            if (WhichDatabase == "Local")
            {
                var flags = await _firstDbContext.Flags.Where(o => o.ProjectId == ProjectId).ToListAsync();

                if (flags == null)
                {
                    return NotFound();
                }

                return flags;
            }
            else
            {
                var flags = await _secondDbContext.Flags.Where(o => o.ProjectId == ProjectId).ToListAsync();

                if (flags == null)
                {
                    return NotFound();
                }

                return flags;
            }

        }

        // PUT: api/Flags/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutFlag(int id, Flag flag, string WhichDatabase)
        {
            if (id != flag.FlagId)
            {
                return BadRequest();
            }
            if (WhichDatabase == "Local")
            {

                _firstDbContext.Entry(flag).State = EntityState.Modified;
                var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    _logger.LogEvent($"Flag : {id} Updated", "Flag", userId, WhichDatabase);
                }

                try
                {
                    await _firstDbContext.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!FlagExists(id, WhichDatabase))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }

            }
            else
            {

                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                _secondDbContext.Entry(flag).State = EntityState.Modified;
                var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    _logger.LogEvent($"Flag : {id} Updated", "Flag", userId, WhichDatabase);
                }

                try
                {
                    await _secondDbContext.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!FlagExists(id, WhichDatabase))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            return NoContent();
        }

        private bool FlagExists(int id, string WhichDatabase)
        {
            if (WhichDatabase == "Local")
            {
                return _firstDbContext.Flags.Any(e => e.FlagId == id);
            }
            else
            {
                if (_connectionChecker.IsOnlineDatabaseAvailable())
                {
                    return _secondDbContext.Flags.Any(e => e.FlagId == id);
                }
                return false;
            }
        }
    }
}
