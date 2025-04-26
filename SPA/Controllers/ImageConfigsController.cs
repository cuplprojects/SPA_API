using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Office2010.Excel;
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
    public class ImageConfigsController : ControllerBase
    {
        private readonly FirstDbContext _firstDbcontext;
        private readonly SecondDbContext _secondDbContext;
        private readonly IChangeLogger _changeLogger;
        private readonly ISecurityService _securityService;
        private readonly DatabaseConnectionChecker _connectionChecker;
        private readonly ILoggerService _logger;

        public ImageConfigsController(FirstDbContext firstDbcontext, SecondDbContext secondDbContext, IChangeLogger changeLogger, ISecurityService securityService, DatabaseConnectionChecker databaseConnectionChecker, ILoggerService logger)
        {
            _firstDbcontext = firstDbcontext;
            _secondDbContext = secondDbContext;
            _changeLogger = changeLogger;
            _securityService = securityService;
            _connectionChecker = databaseConnectionChecker;
            _logger = logger;
        }

        // GET: api/ImageConfigs
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ImageConfig>>> GetImageConfigs(string WhichDatabase)
        {
            if (WhichDatabase == "Local")
            {
                return await _firstDbcontext.ImageConfigs.ToListAsync();
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                return await _secondDbContext.ImageConfigs.ToListAsync();
            }
        }

        // GET: api/ImageConfigs/5
        [HttpGet("{id}")]
        public async Task<ActionResult<ImageConfig>> GetImageConfig(int id, string WhichDatabase)
        {
            if (WhichDatabase == "Local")
            {
                var imageconfig = await _firstDbcontext.ImageConfigs.FindAsync(id);
                if (imageconfig == null)
                {
                    return NotFound();
                }
                return imageconfig;
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                var imageconfig = await _secondDbContext.ImageConfigs.FindAsync(id);
                if (imageconfig == null)
                {
                    return NotFound();
                }
                return imageconfig;
            }
        }

      

        [HttpPut("{id}")]
        public async Task<IActionResult> PutImageConfig(int id, Cyphertext cyphertext, string WhichDatabase)
        {
            // Decrypt the cyphertext
            string decryptedJson = _securityService.Decrypt(cyphertext.cyphertextt);

            // Debug: Log the decrypted JSON
            Console.WriteLine("Decrypted JSON: " + decryptedJson);

            // Deserialize the decrypted JSON into the ImageConfig object
            var imageConfig = JsonConvert.DeserializeObject<ImageConfig>(decryptedJson);

            // Validate the provided ID with the deserialized object's ID
            if (id != imageConfig.Id)
            {
                return BadRequest();
            }

            if (WhichDatabase == "Local")
            {
                _firstDbcontext.Entry(imageConfig).State = EntityState.Modified;
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                _secondDbContext.Entry(imageConfig).State = EntityState.Modified;
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
                if (!ImageConfigExists(id, WhichDatabase))
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
                _logger.LogEvent($"Image Config : {id} Updated", "Image Configuration", userId, WhichDatabase);
            }


            return NoContent();
        }

        [HttpPost]
        public async Task<ActionResult<ImageConfig>> PostImageConfig(Cyphertext cyphertext, string WhichDatabase)
        {
            // Decrypt the cyphertext
            string decryptedJson = _securityService.Decrypt(cyphertext.cyphertextt);

            // Debug: Log the decrypted JSON
            Console.WriteLine("Decrypted JSON: " + decryptedJson);

            // Deserialize the decrypted JSON into the ImageConfig object
            var imageConfig = JsonConvert.DeserializeObject<ImageConfig>(decryptedJson);

            // Assign a new ID to the ImageConfig object
            int newImgConId = GetNextImgConId(WhichDatabase);
            imageConfig.Id = newImgConId;

            if (WhichDatabase == "Local")
            {
                _firstDbcontext.ImageConfigs.Add(imageConfig);
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                _secondDbContext.ImageConfigs.Add(imageConfig);
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
                if (ImageConfigExists(imageConfig.Id, WhichDatabase))
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
                _logger.LogEvent($"Image Config : {imageConfig.Id} Added", "Image Configuration", userId, WhichDatabase);
            }

            return CreatedAtAction("GetImageConfig", new { id = imageConfig.Id }, imageConfig);
        }


        // DELETE: api/ImageConfigs/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteImageConfig(int id, string WhichDatabase)
        {
            if (WhichDatabase == "Local")
            {
                var imageConfig = await _firstDbcontext.ImageConfigs.FindAsync(id);
                if (imageConfig == null)
                {
                    return NotFound();
                }
                _firstDbcontext.ImageConfigs.Remove(imageConfig);
                var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    _logger.LogEvent($"Image Config : {id} Deleted", "Image Configuration", userId, WhichDatabase);
                }
                await _firstDbcontext.SaveChangesAsync();
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                var imageConfig = await _secondDbContext.ImageConfigs.FindAsync(id);
                if (imageConfig == null)
                {
                    return NotFound();
                }
                _secondDbContext.ImageConfigs.Remove(imageConfig);
                var userIdClaim = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    _logger.LogEvent($"Image Config : {id} Deleted", "Image Configuration", userId, WhichDatabase);
                }
                await _secondDbContext.SaveChangesAsync();
            }

            return NoContent();
        }

    
        [HttpGet("ByProjectId/{projectId}")]
        public async Task<ActionResult<string>> GetImageConfigsByProjectId(int projectId, string WhichDatabase)
        {
            List<ImageConfig> imageConfigs = new List<ImageConfig>();

            if (WhichDatabase == "Local")
            {
                imageConfigs = await _firstDbcontext.ImageConfigs.Where(config => config.ProjectId == projectId).ToListAsync();
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online database is not available.");
                }
                imageConfigs = await _secondDbContext.ImageConfigs.Where(config => config.ProjectId == projectId).ToListAsync();
            }

            if (imageConfigs == null || imageConfigs.Count == 0)
            {
                return NotFound();
            }

            // Serialize the list of ImageConfig objects to JSON
            string jsonResponse = JsonConvert.SerializeObject(imageConfigs);

            // Encrypt the JSON response
            string encryptedResponse = _securityService.Encrypt(jsonResponse);

            return Ok(encryptedResponse);
        }

        private bool ImageConfigExists(int id, string WhichDatabase)
        {
            if (WhichDatabase == "Local")
            {
                return _firstDbcontext.ImageConfigs.Any(e => e.Id == id);
            }
            else
            {
                if (_connectionChecker.IsOnlineDatabaseAvailable())
                {
                    return _secondDbContext.ImageConfigs.Any(e => e.Id == id);
                }
                return false;
            }
        }

        private int GetNextImgConId(string whichDatabase)
        {
            return whichDatabase == "Local" ? _firstDbcontext.ImageConfigs.Max(c => (int?)c.Id) + 1 ?? 1 : _secondDbContext.ImageConfigs.Max(c => (int?)c.Id) + 1 ?? 1;
        }
    }
}
