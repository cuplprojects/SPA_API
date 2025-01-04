using System.Collections.Generic;
using System.Drawing.Printing;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Newtonsoft.Json;
using SPA;
using SPA.Data;
using SPA.Services;

namespace SPA.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class RolesController : ControllerBase
    {
        private readonly FirstDbContext _firstDbContext;
        private readonly SecondDbContext _secondDbContext;
        private readonly IChangeLogger _changeLogger;
        private readonly DatabaseConnectionChecker _connectionChecker;
        private readonly ILoggerService _logger;

        public RolesController(FirstDbContext firstDbcontext, SecondDbContext secondDbContext, IChangeLogger changeLogger, DatabaseConnectionChecker connectionChecker, ILoggerService logger)
        {
            _firstDbContext = firstDbcontext;
            _secondDbContext = secondDbContext;
            _changeLogger = changeLogger;
            _connectionChecker = connectionChecker;
            _logger = logger;
        }

        // GET: api/Roles
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Role>>> GetRoles(string WhichDatabase)
        {

            if (WhichDatabase == "Local")
            {
                return await _firstDbContext.Roles.ToListAsync();
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                return await _secondDbContext.Roles.ToListAsync();
            }
        }

        // GET: api/Roles/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Role>> GetRole(int id, string WhichDatabase)
        {
            var role = new Role();
            if (WhichDatabase == "Local")
            {
                role = await _firstDbContext.Roles.FindAsync(id);
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                role = await _secondDbContext.Roles.FindAsync(id);
            }

            if (role == null)
            {
                return NotFound();
            }

            return role;
        }

        // PUT: api/Roles/5
        // PUT: api/Roles/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutRole(int id, [FromBody] Role role, string WhichDatabase)
        {
            if (WhichDatabase == "Local")
            {
                if (id != role.RoleId)
                {
                    return BadRequest();
                }
                _firstDbContext.Entry(role).State = EntityState.Modified;
            }
            else
            {
                if (id != role.RoleId)
                {
                    return BadRequest();
                }
                _secondDbContext.Entry(role).State = EntityState.Modified;
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
                if (!RoleExists(id, WhichDatabase))
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
                _logger.LogEvent($"Role : {id} Updated", "Role", userId, WhichDatabase);
            }
            return NoContent();
        }


        // POST: api/Roles
        [HttpPost]
        public async Task<ActionResult<Role>> PostRole([FromBody] Role role, string WhichDatabase)
        {
           
            int newRoleId = GetNextRoleId(WhichDatabase);
            role.RoleId = newRoleId;

            if (WhichDatabase == "Local")
            {
                role.Permission = JsonConvert.SerializeObject(role.PermissionList);
                _firstDbContext.Roles.Add(role);
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                role.Permission = JsonConvert.SerializeObject(role.PermissionList);
                _secondDbContext.Roles.Add(role);
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
                if (RoleExists(role.RoleId, WhichDatabase))
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
                _logger.LogEvent($"Role : {role.RoleName} Added", "Role", userId, WhichDatabase);
            }

            return CreatedAtAction(nameof(GetRole), new { id = role.RoleId }, role);
        }

        // DELETE: api/Roles/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRole(int id, string WhichDatabase)
        {
            if (WhichDatabase == "Local")
            {
                var role = await _firstDbContext.Roles.FindAsync(id);
                if (role == null)
                {
                    return NotFound();
                }
                _firstDbContext.Roles.Remove(role);
                var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    _logger.LogEvent($"Role : {role.RoleName} Deleted", "Role", userId, WhichDatabase);
                }
                await _firstDbContext.SaveChangesAsync();
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                var role = await _secondDbContext.Roles.FindAsync(id);
                if (role == null)
                {
                    return NotFound();
                }
                _secondDbContext.Roles.Remove(role);
                var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    _logger.LogEvent($"Role : {role.RoleName} Deleted", "Role", userId, WhichDatabase);
                }
                await _secondDbContext.SaveChangesAsync();
            }

            return NoContent();
        }

        private bool RoleExists(int id, string WhichDatabase)
        {
            if (WhichDatabase == "Local")
            {
                return _firstDbContext.Roles.Any(e => e.RoleId == id);
            }
            else
            {
                if (_connectionChecker.IsOnlineDatabaseAvailable())
                {
                    return _secondDbContext.Roles.Any(e => e.RoleId == id);
                }
                return false;
            }
        }

        private int GetNextRoleId(string WhichDatabase)
        {
            return WhichDatabase == "Local" ? _firstDbContext.Roles.Max(c => (int?)c.RoleId) + 1 ?? 1 : _secondDbContext.Roles.Max(c => (int?)c.RoleId) + 1 ?? 1;
        }
    }
}
