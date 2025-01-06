using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.InkML;
using DocumentFormat.OpenXml.Office2010.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Build.Evaluation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SPA;
using SPA.Data;
using SPA.Models;
using SPA.Models.NonDBModels;
using SPA.Services;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace SPA.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ProjectsController : ControllerBase
    {
        private readonly FirstDbContext _firstDbContext;
        private readonly SecondDbContext _secondDbContext;
        private readonly IChangeLogger _changeLogger;
        private readonly DatabaseConnectionChecker _connectionChecker;
        private readonly ILoggerService _logger;

        public ProjectsController(FirstDbContext firstDbContext, SecondDbContext secondDbContext, IChangeLogger changeLogger, DatabaseConnectionChecker connectionChecker, ILoggerService logger)
        {
            _firstDbContext = firstDbContext;
            _secondDbContext = secondDbContext;
            _changeLogger = changeLogger;
            _connectionChecker = connectionChecker;
            _logger = logger;
        }


        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetProject(string WhichDatabase)
        {
            try
            {
                // Fetch projects from the appropriate database
                List<Project> projects;
                List<User> users;

                if (WhichDatabase == "Local")
                {
                    projects = await _firstDbContext.Projects.ToListAsync();
                    users = await _firstDbContext.Users.ToListAsync();
                }
                else
                {
                    if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                    {
                        return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                    }
                    projects = await _secondDbContext.Projects.ToListAsync();
                    users = await _secondDbContext.Users.ToListAsync();
                }

                // Map user IDs to full names
                var userDictionary = users.ToDictionary(u => u.UserId, u => $"{u.FirstName} {u.LastName}");

                // Map projects to include full names of assigned users
                var projectsWithUserNames = projects.Select(project => new
                {
                    project.ProjectId,
                    project.ProjectName,
                    UserAssigned = project.UserAssigned
                        .Select(userId => userDictionary.ContainsKey(userId) ? userDictionary[userId] : "Unknown User")
                        .ToList()
                }).ToList();

                return Ok(projectsWithUserNames);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, $"Error retrieving projects with users: {ex.Message}");
            }
        }

        [HttpGet("users/{projectId}")]
        public async Task<ActionResult<IEnumerable<object>>> GetUsersByProjectId(string WhichDatabase, int projectId)
        {
            try
            {
                List<Project> projects;
                List<User> users;

                // Fetch projects and users from the appropriate database
                if (WhichDatabase == "Local")
                {
                    projects = await _firstDbContext.Projects.ToListAsync();
                    users = await _firstDbContext.Users.ToListAsync();
                }
                else
                {
                    if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                    {
                        return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                    }
                    projects = await _secondDbContext.Projects.ToListAsync();
                    users = await _secondDbContext.Users.ToListAsync();
                }

                // Find the project with the specified projectId
                var project = projects.FirstOrDefault(p => p.ProjectId == projectId);

                if (project == null)
                {
                    return NotFound($"Project with ID {projectId} not found.");
                }

                // Get the users assigned to the project
                var userIds = project.UserAssigned;
                var assignedUsers = users.Where(u => userIds.Contains(u.UserId)).Select(u => new
                {
                    u.UserId,
                    FullName = $"{u.FirstName} {u.LastName}"
                }).ToList();

                return Ok(assignedUsers);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, $"Error retrieving users for project {projectId}: {ex.Message}");
            }
        }

        // GET: api/Projects
        [HttpGet("YourProject")]
        public async Task<ActionResult<IEnumerable<object>>> GetProjectsWithUsers(string WhichDatabase, int userId)
        {
            try
            {
                List<Project> projects;

                if (WhichDatabase == "Local")
                {
                    projects = await _firstDbContext.Projects
                        .ToListAsync();
                    var allUserIds = projects.SelectMany(p => p.UserAssigned).Distinct().ToList();

                    // Query to get users with matching UserIds
                    var users = await _firstDbContext.Users
                        .Where(u => allUserIds.Contains(u.UserId))
                        .ToListAsync();

                    // Get archived project IDs for the specified userId
                    var archivedProjectIds = await _firstDbContext.ProjectArchives
                        .Where(h => h.UserId == userId)
                        .Select(h => h.ProjectId)
                        .ToListAsync();

                    // Filter out archived projects
                    projects = projects.Where(p => !archivedProjectIds.Contains(p.ProjectId)).ToList();

                    // Query to join Projects with Users based on UserAssignedIds and Users
                    var projectswithusers = (
                        from project in projects
                        select new
                        {
                            project.ProjectId,
                            project.ProjectName,
                            UserAssigned = users.Where(u => project.UserAssigned.Contains(u.UserId))
                                                .Select(u => $"{u.FirstName} {u.LastName}")
                                                .ToList()
                        }
                    ).ToList();
                    return Ok(projectswithusers);
                }
                else
                {
                    if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                    {
                        return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                    }
                    projects = await _secondDbContext.Projects
                        .ToListAsync();
                    var allUserIds = projects.SelectMany(p => p.UserAssigned).Distinct().ToList();

                    // Query to get users with matching UserIds
                    var users = await _secondDbContext.Users
                        .Where(u => allUserIds.Contains(u.UserId))
                        .ToListAsync();

                    // Get archived project IDs for the specified userId
                    var archivedProjectIds = await _secondDbContext.ProjectArchives
                        .Where(h => h.UserId == userId)
                        .Select(h => h.ProjectId)
                        .ToListAsync();

                    // Filter out archived projects
                    projects = projects.Where(p => !archivedProjectIds.Contains(p.ProjectId)).ToList();

                    // Query to join Projects with Users based on UserAssignedIds and Users
                    var projectswithusers = (
                        from project in projects
                        select new
                        {
                            project.ProjectId,
                            project.ProjectName,
                            UserAssigned = users.Where(u => project.UserAssigned.Contains(u.UserId))
                                                .Select(u => $"{u.FirstName} {u.LastName}")
                                                .ToList()
                        }
                    ).ToList();
                    return Ok(projectswithusers);
                }

                // Get all unique UserIds from all projects

            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, $"Error retrieving projects with users: {ex.Message}");
            }
        }

        [HttpGet("ArchivedByUser")]
        public async Task<ActionResult<IEnumerable<object>>> GetArchivedProjectsByUser(int userId, string WhichDatabase)
        {
            if (userId < 0)
            {
                return BadRequest("UserId is required");
            }

            try
            {
                List<ProjectArchive> archivedProjects;

                if (WhichDatabase == "Local")
                {
                    archivedProjects = await _firstDbContext.ProjectArchives
                        .Where(pa => pa.UserId == userId)
                        .ToListAsync();
                }
                else
                {
                    if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                    {
                        return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                    }
                    archivedProjects = await _secondDbContext.ProjectArchives
                        .Where(pa => pa.UserId == userId)
                        .ToListAsync();
                }

                if (archivedProjects == null || archivedProjects.Count == 0)
                {
                    return NotFound("No archived projects found for the user.");
                }

                var projectIds = archivedProjects
                    .GroupBy(ap => ap.ProjectId)
                    .Select(g => g.First().ProjectId)
                    .ToList();

                List<Project> projects;

                if (WhichDatabase == "Local")
                {
                    projects = await _firstDbContext.Projects
                        .Where(p => projectIds.Contains(p.ProjectId))
                        .ToListAsync();
                }
                else
                {
                    if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                    {
                        return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                    }
                    projects = await _secondDbContext.Projects
                        .Where(p => projectIds.Contains(p.ProjectId))
                        .ToListAsync();
                }

                var allUserIds = projects.SelectMany(p => p.UserAssigned).Distinct().ToList();

                List<User> users;

                if (WhichDatabase == "Local")
                {
                    users = await _firstDbContext.Users
                        .Where(u => allUserIds.Contains(u.UserId))
                        .ToListAsync();
                }
                else
                {
                    if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                    {
                        return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                    }
                    users = await _secondDbContext.Users
                        .Where(u => allUserIds.Contains(u.UserId))
                        .ToListAsync();
                }

                var result = projects.Select(p => new
                {
                    p.ProjectId,
                    p.ProjectName,
                    UserAssigned = users.Where(u => p.UserAssigned.Contains(u.UserId))
                                        .Select(u => $"{u.FirstName} {u.LastName}")
                                        .ToList()
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, $"Error retrieving archived projects: {ex.Message}");
            }
        }


        [HttpGet("ByUser/{userId}")]
        public async Task<ActionResult<IEnumerable<Project>>> GetProjectsByUserId(int userId, string WhichDatabase)
        {
            if (userId < 0)
            {
                return BadRequest("Invalid userId");
            }

            IQueryable<Project> query;

            if (WhichDatabase == "Local")
            {
                query = _firstDbContext.Projects;
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                query = _secondDbContext.Projects;
            }

            try
            {
                // Fetch all projects and then filter in memory
                var allProjects = await query.ToListAsync();

                // Filter projects based on userId
                var projectsByUser = allProjects
                    .Where(p => p.UserAssigned.Contains(userId))
                    .OrderByDescending(p=>p.ProjectId)
                    .ToList();

                if (projectsByUser == null || !projectsByUser.Any())
                {
                    return NotFound("No projects found for the specified user");
                }

                return Ok(projectsByUser);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, $"Internal server error: {ex.Message}");
            }
        }

        // GET: api/Projects/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Project>> GetProject(int id, string WhichDatabase)
        {


            if (WhichDatabase == "Local")
            {
                var project = await _firstDbContext.Projects.FindAsync(id);
                if (project == null)
                {
                    return NotFound();
                }
                return project;
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                var project = await _secondDbContext.Projects.FindAsync(id);
                if (project == null)
                {
                    return NotFound();
                }
                return project;
            }
        }

        // PUT: api/Projects/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutProject(int id, Project project, string WhichDatabase)
        {
            if (WhichDatabase == "Local")
            {
                if (id != project.ProjectId)
                {
                    return BadRequest();
                }
                _firstDbContext.Entry(project).State = EntityState.Modified;

            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                if (id != project.ProjectId)
                {
                    return BadRequest();
                }
                _secondDbContext.Entry(project).State = EntityState.Modified;
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
                if (!ProjectExists(id, WhichDatabase))
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
                _logger.LogEvent($"Project : {id} Updated", "Project", userId, WhichDatabase);
            }

            return NoContent();
        }

        // POST: api/Projects
        [HttpPost]
        public async Task<ActionResult<Project>> PostProject(Project project, string WhichDatabase)
        {
            if (WhichDatabase == "Local")
            {
                project.ProjectId = GetNextProjectId(WhichDatabase);
                _firstDbContext.Projects.Add(project);
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                project.ProjectId = GetNextProjectId(WhichDatabase);
                _secondDbContext.Projects.Add(project);
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
                if (ProjectExists(project.ProjectId, WhichDatabase))
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
                _logger.LogEvent($"Project : {project.ProjectName} Added", "Project", userId, WhichDatabase);
            }

            return CreatedAtAction("GetProject", new { id = project.ProjectId }, project);
        }

        // DELETE: api/Projects/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProject(int id, string WhichDatabase)
        {
            if (WhichDatabase == "Local")
            {
                var project = await _firstDbContext.Projects.FindAsync(id);
                if (project == null)
                {
                    return NotFound();
                }

                _firstDbContext.Projects.Remove(project);
                var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    _logger.LogEvent($"Project : {project.ProjectName} Deleted", "Project", userId, WhichDatabase);
                }
                await _firstDbContext.SaveChangesAsync();
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                var project = await _secondDbContext.Projects.FindAsync(id);
                if (project == null)
                {
                    return NotFound();
                }

                _secondDbContext.Projects.Remove(project);
                var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    _logger.LogEvent($"Project : {project.ProjectName} Deleted", "Project", userId, WhichDatabase);
                }
                await _secondDbContext.SaveChangesAsync();
            }

            return NoContent();



        }

        [HttpPost("{id}/archive")]
        public async Task<IActionResult> ArchiveProject(int id, int userId, string WhichDatabase)
        {
            if (WhichDatabase == "Local")
            {
                try
                {
                    var project = await _firstDbContext.Projects.FindAsync(id);
                    if (project == null)
                    {
                        return NotFound();
                    }

                    // Check if the project is already archived by the user
                    var existingArchiveRecord = await _firstDbContext.ProjectArchives
                        .FirstOrDefaultAsync(pa => pa.ProjectId == id && pa.UserId == userId);

                    if (existingArchiveRecord != null)
                    {
                        return BadRequest("This project is already archived by the user.");
                    }

                    // Archive the project by adding a record to ProjectArchives
                    var archiveRecord = new ProjectArchive
                    {
                        ProjectId = id,
                        UserId = userId,
                        ArchiveDate = DateTime.UtcNow
                    };
                    _firstDbContext.ProjectArchives.Add(archiveRecord);
                    var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                    if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userID))
                    {
                        _logger.LogEvent($"Project : {project.ProjectName} Archived", "Project", userId, WhichDatabase);
                    }
                    await _firstDbContext.SaveChangesAsync();

                    return Ok("Project archived successfully");
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"Internal server error: {ex.Message}");
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
                    var project = await _secondDbContext.Projects.FindAsync(id);
                    if (project == null)
                    {
                        return NotFound();
                    }

                    // Check if the project is already archived by the user
                    var existingArchiveRecord = await _secondDbContext.ProjectArchives
                        .FirstOrDefaultAsync(pa => pa.ProjectId == id && pa.UserId == userId);

                    if (existingArchiveRecord != null)
                    {
                        return BadRequest("This project is already archived by the user.");
                    }

                    // Archive the project by adding a record to ProjectArchives
                    var archiveRecord = new ProjectArchive
                    {
                        ProjectId = id,
                        UserId = userId,
                        ArchiveDate = DateTime.UtcNow
                    };
                    _secondDbContext.ProjectArchives.Add(archiveRecord);
                    var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                    if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userID))
                    {
                        _logger.LogEvent($"Project : {project.ProjectName} Archived", "Project", userId, WhichDatabase);
                    }
                    await _secondDbContext.SaveChangesAsync();

                    return Ok("Project archived successfully");
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"Internal server error: {ex.Message}");
                }
            }
        }

        [HttpPost("{id}/unarchive")]
        public async Task<IActionResult> UnarchiveProject(int id, int userId, string WhichDatabase)
        {
            if (WhichDatabase == "Local")
            {
                try
                {

                    // Check if the project is archived by the specified user 
                    var archiveRecord = await _firstDbContext.ProjectArchives
                        .FirstOrDefaultAsync(t => t.ProjectId == id && t.UserId == userId);

                    if (archiveRecord == null)
                    {
                        return NotFound("Project not archived by the user");
                    }
                    var projectName =  await _firstDbContext.Projects.FirstOrDefaultAsync(t => t.ProjectId == archiveRecord.ProjectId);

                    // Unarchive the project by removing the archive record 
                    _firstDbContext.ProjectArchives.Remove(archiveRecord);
                    var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                    if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userID))
                    {
                        _logger.LogEvent($"Project : {projectName} Unarchived", "Project", userId, WhichDatabase);
                    }
                    await _firstDbContext.SaveChangesAsync();

                    return Ok("Project unarchived successfully");
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"Internal server error: {ex.Message}");
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

                    // Check if the project is archived by the specified user 
                    var archiveRecord = await _secondDbContext.ProjectArchives
                        .FirstOrDefaultAsync(t => t.ProjectId == id && t.UserId == userId);

                    if (archiveRecord == null)
                    {
                        return NotFound("Project not archived by the user");
                    }
                    var projectName = await _firstDbContext.Projects.FirstOrDefaultAsync(t => t.ProjectId == archiveRecord.ProjectId);

                    // Unarchive the project by removing the archive record 
                    _secondDbContext.ProjectArchives.Remove(archiveRecord);
                    var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                    if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userID))
                    {
                        _logger.LogEvent($"Project : {projectName} Unarchived", "Project", userId, WhichDatabase);
                    }
                    await _secondDbContext.SaveChangesAsync();

                    return Ok("Project unarchived successfully");
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"Internal server error: {ex.Message}");
                }
            }

        }

        [HttpGet("GetProjectCounts")]
        public async Task<IActionResult> ProjectsCounts(int ProjectId, string CategoryName, string WhichDatabase)
        {
            int counts = 0;
            IQueryable<object> queryable = null;

            if (WhichDatabase == "Local")
            {
                switch (CategoryName)
                {
                    case "Scanned":
                        queryable = _firstDbContext.OMRdatas.Where(c => c.ProjectId == ProjectId);
                        break;
                    case "Registration":
                        queryable = _firstDbContext.RegistrationDatas.Where(c => c.ProjectId == ProjectId);
                        break;
                    case "Images":
                        queryable = _firstDbContext.OMRImages.Where(c => c.ProjectId == ProjectId);
                        break;
                    case "Keys":
                        queryable = _firstDbContext.Keyss.Where(c => c.ProjectId == ProjectId);
                        break;
                    case "Absentees":
                        queryable = _firstDbContext.Absentees.Where(c => c.ProjectID == ProjectId);
                        break;
                    case "FieldConfig":
                        queryable = _firstDbContext.FieldConfigs.Where(c => c.ProjectId == ProjectId);
                        break;
                    case "ResponseConfig":
                        queryable = _firstDbContext.ResponseConfigs.Where(c => c.ProjectId == ProjectId);
                        break;
                    case "ImageConfig":
                        queryable = _firstDbContext.ImageConfigs.Where(c => c.ProjectId == ProjectId);
                        break;
                    // Add more cases for other categories
                    default:
                        return BadRequest("Invalid CategoryName");
                }
            }
            else
            {
                switch (CategoryName)
                {
                    case "Scanned":
                        queryable = _secondDbContext.OMRdatas.Where(c => c.ProjectId == ProjectId);
                        break;
                    case "Registration":
                        queryable = _secondDbContext.RegistrationDatas.Where(c => c.ProjectId == ProjectId);
                        break;
                    case "Images":
                        queryable = _secondDbContext.OMRImages.Where(c => c.ProjectId == ProjectId);
                        break;
                    case "Keys":
                        queryable = _secondDbContext.Keyss.Where(c => c.ProjectId == ProjectId);
                        break;
                    case "Absentees":
                        queryable = _secondDbContext.Absentees.Where(c => c.ProjectID == ProjectId);
                        break;
                    case "FieldConfig":
                        queryable = _secondDbContext.FieldConfigs.Where(c => c.ProjectId == ProjectId);
                        break;
                    case "ResponseConfig":
                        queryable = _secondDbContext.ResponseConfigs.Where(c => c.ProjectId == ProjectId);
                        break;
                    case "ImageConfig":
                        queryable = _secondDbContext.ImageConfigs.Where(c => c.ProjectId == ProjectId);
                        break;
                    // Add more cases for other categories
                    default:
                        return BadRequest("Invalid CategoryName");
                }
            }

            if (queryable != null)
            {
                counts = await queryable.CountAsync();
            }

            return Ok(counts);
        }

        private int GetNextProjectId(string WhichDatabase)
        {
            return WhichDatabase == "Local" ? _firstDbContext.Projects.Max(c => (int?)c.ProjectId) + 1 ?? 1 : _secondDbContext.Projects.Max(c => (int?)c.ProjectId) + 1 ?? 1;
        }

        private bool ProjectExists(int id, string WhichDatabase)
        {
            if (WhichDatabase == "Local")
            {
                return _firstDbContext.Projects.Any(e => e.ProjectId == id);
            }
            else
            {
                if (_connectionChecker.IsOnlineDatabaseAvailable())
                {
                    return _secondDbContext.Projects.Any(e => e.ProjectId == id);
                }
                return false;
            }

        }
    }
}
