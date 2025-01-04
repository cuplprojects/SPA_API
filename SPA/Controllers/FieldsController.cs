using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
    public class FieldsController : ControllerBase
    {
        private readonly FirstDbContext _firstDbContext;
        private readonly SecondDbContext _secondDbContext;
        private readonly IChangeLogger _changeLogger;
        private readonly DatabaseConnectionChecker _connectionChecker;
        private readonly ILoggerService _logger;


        public FieldsController(FirstDbContext firstDbContext, SecondDbContext secondDbContext, IChangeLogger changeLogger, DatabaseConnectionChecker connectionChecker, ILoggerService logger)
        {
            _firstDbContext = firstDbContext;
            _secondDbContext = secondDbContext;
            _changeLogger = changeLogger;
            _connectionChecker = connectionChecker;
            _logger = logger;
        }

        // GET: api/Fields
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Field>>> GetFields(string WhichDatabase)
        {
            if (WhichDatabase == "Local")
            {
                return await _firstDbContext.Fields.ToListAsync();
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                return await _secondDbContext.Fields.ToListAsync();

            }
        }

        // GET: api/Fields/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Field>> GetField(int id, string WhichDatabase)
        {
            if (WhichDatabase == "Local")
            {
                var field = await _firstDbContext.Fields.FindAsync(id);

                if (field == null)
                {
                    return NotFound();
                }
                return field;
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                var field = await _secondDbContext.Fields.FindAsync(id);

                if (field == null)
                {
                    return NotFound();
                }
                return field;
            }

        }

        // PUT: api/Fields/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutField(int id, Field field, string WhichDatabase)
        {
            if (WhichDatabase == "Local")
            {
                if (id != field.FieldId)
                {
                    return BadRequest();
                }
                _firstDbContext.Entry(field).State = EntityState.Modified;
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                if (id != field.FieldId)
                {
                    return BadRequest();
                }
                _secondDbContext.Entry(field).State = EntityState.Modified;
            }

            try
            {
                if (WhichDatabase == "Local")
                {
                    await _firstDbContext.SaveChangesAsync();
                }
                else
                {
                    await _secondDbContext.SaveChangesAsync();
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!FieldExists(id, WhichDatabase))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                _logger.LogEvent($"Field {id} Updated ", "Field", userId, WhichDatabase);
            }

            return NoContent();
        }

        // POST: api/Fields
        [HttpPost]
        public async Task<ActionResult<Field>> PostField(Field field, string WhichDatabase)
        {
            if (WhichDatabase == "Local")
            {
                _firstDbContext.Fields.Add(field);
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                _secondDbContext.Fields.Add(field);
            }

            try
            {
                if (WhichDatabase == "Local")
                {
                    await _firstDbContext.SaveChangesAsync();
                }
                else
                {
                    await _secondDbContext.SaveChangesAsync();
                }
            }
            catch (DbUpdateException)
            {
                if (FieldExists(field.FieldId, WhichDatabase))
                {
                    return Conflict();
                }
                else
                {
                    throw;
                }
            }

            var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                _logger.LogEvent($"Field : {field.FieldName} Added ", "Field", userId, WhichDatabase);
            }
            return CreatedAtAction("GetFields", new { id = field.FieldId }, field);
        }

        // DELETE: api/Fields/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteField(int id, string WhichDatabase)
        {
            if (WhichDatabase == "Local")
            {
                var field = await _firstDbContext.Fields.FindAsync(id);

                if (field == null)
                {
                    return NotFound();

                }
                _firstDbContext.Fields.Remove(field);
                var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    _logger.LogEvent($"Field : {field.FieldName} Deleted ", "Field", userId, WhichDatabase);
                }
                await _firstDbContext.SaveChangesAsync();
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                var field = await _firstDbContext.Fields.FindAsync(id);

                if (field == null)
                {
                    return NotFound();

                }

                _secondDbContext.Fields.Remove(field);
                var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    _logger.LogEvent($"Field : {field.FieldName} Deleted ", "Field", userId, WhichDatabase);
                }
                await _secondDbContext.SaveChangesAsync();
            }
            return NoContent();
        }

        private bool FieldExists(int id, string WhichDatabase)
        {
            if (WhichDatabase == "Local")
            {
                return _firstDbContext.Fields.Any(e => e.FieldId == id);
            }
            else
            {
                if (_connectionChecker.IsOnlineDatabaseAvailable())
                {

                    return _secondDbContext.Fields.Any(e => e.FieldId == id);
                }
                return false;
            }

        }
    }
}
