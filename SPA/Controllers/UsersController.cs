using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SPA;
using SPA.Data;
using SPA.Models;
using SPA.Services;
using SPA.Encryptions;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Authorization;
using System.Net.WebSockets;
using SPA.Models.NonDBModels;
using System.Security.Claims;
using Microsoft.CodeAnalysis;

namespace SPA.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private static int _globalUserIdCounter;
        private static int _globalUserAuthIdCounter;
        private readonly FirstDbContext _firstDbcontext;
        private readonly SecondDbContext _secondDbContext;
        private readonly IChangeLogger _changeLogger;
        private readonly IConfiguration _configuration;
        private readonly ISecurityService _securityService;
        private readonly DatabaseConnectionChecker _connectionChecker;
        private readonly ILoggerService _logger;

        public UsersController(FirstDbContext firstDbcontext, SecondDbContext secondDbContext, IChangeLogger changeLogger, IConfiguration configuration, ISecurityService securityService, DatabaseConnectionChecker connectionChecker, ILoggerService logger)
        {
            _firstDbcontext = firstDbcontext;
            _secondDbContext = secondDbContext;
            _changeLogger = changeLogger;
            _globalUserIdCounter = 0;
            _globalUserAuthIdCounter = 0;
            _configuration = configuration;
            _securityService = securityService;
            _connectionChecker = connectionChecker;
            _logger = logger;
        }

        // GET: api/Users
        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserDto>>> GetUsers(string WhichDatabase)
        {
            try
            {
                List<UserDto> usersWithRoles;

                if (WhichDatabase == "Local")
                {
                    usersWithRoles = await (from user in _firstDbcontext.Users
                                            join role in _firstDbcontext.Roles
                                            on user.RoleId equals role.RoleId
                                            select new UserDto
                                            {
                                                UserId = user.UserId,
                                                FirstName = user.FirstName,
                                                LastName = user.LastName,
                                                FullName = user.FirstName + " " + user.LastName,
                                                Email = user.Email,
                                                RoleId = role.RoleId,
                                                RoleName = role.RoleName,
                                                IsActive = user.IsActive
                                            }).ToListAsync();
                }
                else
                {
                    if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                    {
                        return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                    }
                    // Assuming you have a second DbContext for another database
                    usersWithRoles = await (from user in _secondDbContext.Users
                                            join role in _secondDbContext.Roles
                                            on user.RoleId equals role.RoleId
                                            select new UserDto
                                            {
                                                UserId = user.UserId,
                                                FirstName = user.FirstName,
                                                LastName = user.LastName,
                                                FullName = user.FirstName + " " + user.LastName,
                                                Email = user.Email,
                                                RoleId = role.RoleId,
                                                RoleName = role.RoleName,
                                                IsActive = user.IsActive
                                            }).ToListAsync();
                }

                return Ok(usersWithRoles);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // GET: api/Users/5
        [HttpGet("{id}")]
        public async Task<ActionResult<User>> GetUser(int id, string WhichDatabase)
        {
            if (WhichDatabase == "Local")
            {
                var user = await _firstDbcontext.Users.FindAsync(id);
                if (user == null)
                {
                    return NotFound();
                }
                return user;
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                var user = await _secondDbContext.Users.FindAsync(id);
                if (user == null)
                {
                    return NotFound();
                }
                return user;
            }
        }

        // PUT: api/Users/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutUser(int id, Cyphertext cyphertext, string WhichDatabase)
        {
            
            string decryptedJson = _securityService.Decrypt(cyphertext.cyphertextt);
            User user = JsonConvert.DeserializeObject<User>(decryptedJson);

            if (WhichDatabase == "Local")
            {
                if (id != user.UserId)
                {
                    return BadRequest();
                }
                _firstDbcontext.Entry(user).State = EntityState.Modified;
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                if (id != user.UserId)
                {
                    return BadRequest();
                }
                _secondDbContext.Entry(user).State = EntityState.Modified;
            }
            try
            {
                if (WhichDatabase == "Local")
                {
                    await _firstDbcontext.SaveChangesAsync();
                }
                else
                {
                    await _secondDbContext.SaveChangesAsync();
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!UserExists(id, WhichDatabase))
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
                _logger.LogEvent($"User : {user.UserId},{user.FirstName} {user.LastName} Updated", "User", userId, WhichDatabase);
            }
            return NoContent();
        }

        // POST: api/Users
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<UserResponse>> PostUser(Cyphertext cyphertext, string WhichDatabase)
        {
            
            string decryptedJson = _securityService.Decrypt(cyphertext.cyphertextt);
            User user = JsonConvert.DeserializeObject<User>(decryptedJson);

            int newUserId = GetNextUserId(WhichDatabase);
            user.UserId = newUserId;
            user.IsActive = true;

            if (WhichDatabase == "Local")
            {
                _firstDbcontext.Users.Add(user);
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                _secondDbContext.Users.Add(user);
            }

            try
            {
                if (WhichDatabase == "Local")
                {
                    await _firstDbcontext.SaveChangesAsync();
                }
                else
                {
                    await _secondDbContext.SaveChangesAsync();
                }
            }
            catch (DbUpdateException)
            {
                if (UserExists(user.UserId, WhichDatabase))
                {
                    return Conflict();
                }
                else
                {
                    throw;
                }
            }

            int newAuthId = GetNextUserAuthId(WhichDatabase);

            string generatedPassword = PasswordGenerate.GeneratePassword();
            string hashedPassword = Sha256Hasher.ComputeSHA256Hash(generatedPassword);

            UserAuth userAuth = new UserAuth
            {
                UserAuthId = newAuthId,
                UserId = newUserId,
                Password = hashedPassword,
                AutogenPass = true
            };

            if (WhichDatabase == "Local")
            {
                _firstDbcontext.UserAuths.Add(userAuth);
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                _secondDbContext.UserAuths.Add(userAuth);
            }

            try
            {
                if (WhichDatabase == "Local")
                {
                    await _firstDbcontext.SaveChangesAsync();
                }
                else
                {
                    await _secondDbContext.SaveChangesAsync();
                }
            }
            catch (DbUpdateException)
            {
                if (UserAuthExists(newAuthId, WhichDatabase))
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
                _logger.LogEvent($"User : {user.UserId},{user.FirstName} {user.LastName} Created", "User", userId, WhichDatabase);
                _logger.LogEvent($"UserAuth for : {user.UserId},{user.FirstName} {user.LastName} Created", "UserAuth", userId, WhichDatabase);
            }

            var response = new UserResponse
            {
                User = user,
                GeneratedPassword = generatedPassword
            };

            string emailBody = $@"
                <div style=""text-align: center; background-color: #fff; padding: 20px; border-radius: 8px; box-shadow: 0 0 10px rgba(0, 0, 0, 0.1); border: 2px solid black; min-width: 200px; max-width: 300px; width: 100%; margin: 50px auto;"">
                    <h2 style=""color: blue;"">Login Credentials <hr /></h2>
                     <p>
                        <strong>Username:</strong><br /> {user.Email}
                    </p>
                    <p>
                        <strong>Password:</strong><br /> {generatedPassword}
                    </p>
                    <p style=""color: #F00;"">
                        Please change the password immediately after login.
                    </p>
<div style=""display:flex;align-items:center;justify-content:center;"">
                <a href=""http://spa.edua2z.in"" style=""text-decoration: none; background-color: #007bff; color: #ffffff; padding: 10px 20px; border-radius: 4px; display: inline-block; margin-top: 20px;"">Login</a>
            </div>
                </div>";

            var result = new EmailService(_secondDbContext, _configuration).SendEmail(user.Email, "Welcome to CUPL!", emailBody);

            return CreatedAtAction("GetUser", new { id = user.UserId }, response);
        }

        [HttpPost("WithoutEncryption")]
        public async Task<ActionResult<UserResponse>> PostUserwithoutEncryption(User user, string WhichDatabase)
        {
            
            int newUserId = GetNextUserId(WhichDatabase);
            user.UserId = newUserId;

            if (WhichDatabase == "Local")
            {
                _firstDbcontext.Users.Add(user);
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                _secondDbContext.Users.Add(user);
            }

            try
            {
                if (WhichDatabase == "Local")
                {
                    await _firstDbcontext.SaveChangesAsync();
                }
                else
                {
                    await _secondDbContext.SaveChangesAsync();
                }
            }
            catch (DbUpdateException)
            {
                if (UserExists(user.UserId, WhichDatabase))
                {
                    return Conflict();
                }
                else
                {
                    throw;
                }
            }

            int newAuthId = GetNextUserAuthId(WhichDatabase);

            string generatedPassword = PasswordGenerate.GeneratePassword();
            string hashedPassword = Sha256Hasher.ComputeSHA256Hash(generatedPassword);

            UserAuth userAuth = new UserAuth
            {
                UserAuthId = newAuthId,
                UserId = newUserId,
                Password = hashedPassword,
                AutogenPass = true
            };

            if (WhichDatabase == "Local")
            {
                _firstDbcontext.UserAuths.Add(userAuth);
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                _secondDbContext.UserAuths.Add(userAuth);
            }

            try
            {
                if (WhichDatabase == "Local")
                {
                    await _firstDbcontext.SaveChangesAsync();
                }
                else
                {
                    await _secondDbContext.SaveChangesAsync();
                }
            }
            catch (DbUpdateException)
            {
                if (UserAuthExists(newAuthId, WhichDatabase))
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
                _logger.LogEvent($"User : {user.UserId},{user.FirstName} {user.LastName} Created", "User", userId, WhichDatabase);
                _logger.LogEvent($"UserAuth for : {user.UserId},{user.FirstName} {user.LastName} Created", "UserAuth", userId, WhichDatabase);
            }

            var response = new UserResponse
            {
                User = user,
                GeneratedPassword = generatedPassword
            };

            string emailBody = $@"
                <div style=""text-align: center; background-color: #fff; padding: 20px; border-radius: 8px; box-shadow: 0 0 10px rgba(0, 0, 0, 0.1); border: 2px solid black; min-width: 200px; max-width: 300px; width: 100%; margin: 50px auto;"">
                    <h2 style=""color: blue;"">Login Credentials <hr /></h2>
                     <p>
                        <strong>Username:</strong><br /> {user.Email}
                    </p>
                    <p>
                        <strong>Password:</strong><br /> {generatedPassword}
                    </p>
                    <p style=""color: #F00;"">
                        Please change the password immediately after login.
                    </p>
                </div>";

            var result = new EmailService(_secondDbContext, _configuration).SendEmail(user.Email, "Welcome to CUPL!", emailBody);

            return CreatedAtAction("GetUser", new { id = user.UserId }, response);
        }
        // DELETE: api/Users/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id, string WhichDatabase)
        {
            
            User user;
            UserAuth userAuth;
            if (WhichDatabase == "Local")
            {
                user = await _firstDbcontext.Users.FindAsync(id);
                userAuth = await _firstDbcontext.UserAuths.FirstOrDefaultAsync(u => u.UserId == user.UserId);
                if (user == null)
                {
                    return NotFound();
                }
                _firstDbcontext.Users.Remove(user);
                _firstDbcontext.UserAuths.Remove(userAuth);
                var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    _logger.LogEvent($"User: {user.UserId}, {user.FirstName} {user.LastName} Deleted", "User", userId, WhichDatabase);
                    _logger.LogEvent($"User Auth for : {user.UserId}, {user.FirstName} {user.LastName} Deleted", "UserAuth", userId, WhichDatabase);
                }
                await _firstDbcontext.SaveChangesAsync();
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                user = await _secondDbContext.Users.FindAsync(id);
                userAuth = await _secondDbContext.UserAuths.FirstOrDefaultAsync(u => u.UserId == user.UserId);
                if (user == null)
                {
                    return NotFound();
                }
                _secondDbContext.Users.Remove(user);
                _secondDbContext.UserAuths.Remove(userAuth);
                var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    _logger.LogEvent($"User: {user.UserId}, {user.FirstName} {user.LastName} Deleted", "User", userId, WhichDatabase);
                    _logger.LogEvent($"User Auth for : {user.UserId}, {user.FirstName} {user.LastName} Deleted", "UserAuth", userId, WhichDatabase);
                }
                await _secondDbContext.SaveChangesAsync();
            }
            return NoContent();
        }

        private bool UserExists(int id, string WhichDatabase)
        {
            if (WhichDatabase == "Local")
            {
                return _firstDbcontext.Users.Any(e => e.UserId == id);
            }
            else
            {
                if (_connectionChecker.IsOnlineDatabaseAvailable())
                {
                    return _secondDbContext.Users.Any(e => e.UserId == id);
                }
                return false;
            }
        }
        private bool UserAuthExists(int id, string WhichDatabase)
        {
            if (WhichDatabase == "Local")
            {
                return _firstDbcontext.UserAuths.Any(e => e.UserAuthId == id);
            }
            else
            {
                if (_connectionChecker.IsOnlineDatabaseAvailable())
                {
                    return _secondDbContext.UserAuths.Any(e => e.UserAuthId == id);
                }
                return false;
            }
        }

        private int GetNextUserId(string WhichDatabase)
        {
            return WhichDatabase == "Local" ? _firstDbcontext.Users.Max(c => (int?)c.UserId) + 1 ?? 1 : _secondDbContext.Users.Max(c => (int?)c.UserId) + 1 ?? 1;
        }
        private int GetNextUserAuthId(string WhichDatabase)
        {
            return WhichDatabase == "Local" ? _firstDbcontext.UserAuths.Max(c => (int?)c.UserAuthId) + 1 ?? 1 : _secondDbContext.UserAuths.Max(c => (int?)c.UserAuthId) + 1 ?? 1;
        }

    }

    public class UserResponse
    {
        public User User { get; set; }
        public string GeneratedPassword { get; set; }
    }
}