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
using SPA.Models;
using SPA.Services;

namespace SPA.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class OMRDataStatusController : ControllerBase
    {
        private readonly FirstDbContext _firstDbContext;
        private readonly SecondDbContext _secondDbContext;
        IChangeLogger _changeLogger;

        public OMRDataStatusController(FirstDbContext firstDbContext, SecondDbContext secondDbContext, IChangeLogger changeLogger)
        {
            _firstDbContext = firstDbContext;
            _secondDbContext = secondDbContext;
            _changeLogger = changeLogger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<OMRDataStatus>>> GetOMRDataStatuses(string WhichDatabase)
        {
            if (WhichDatabase == "Local")
            {
                return await _firstDbContext.OMRDataStatuss.ToListAsync();
            }
            else
            {
                return await _secondDbContext.OMRDataStatuss.ToListAsync();

            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<OMRDataStatus>> GetOMRDataStatus(int id, string WhichDatabase)
        {
            if (WhichDatabase == "Local")
            {
                var oMRDataStatus = await _firstDbContext.OMRDataStatuss.FindAsync(id);

                if (oMRDataStatus == null)
                {
                    return NotFound();
                }
                return oMRDataStatus;
            }
            else
            {
                var oMRDataStatus = await _secondDbContext.OMRDataStatuss.FindAsync(id);

                if (oMRDataStatus == null)
                {
                    return NotFound();
                }
                return oMRDataStatus;
            }

        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutOMRDataStatus(int id, OMRDataStatus oMRDataStatus, string WhichDatabase)
        {
            if (WhichDatabase == "Local")
            {
                if (id != oMRDataStatus.StatusId)
                {
                    return BadRequest();
                }
                _firstDbContext.Entry(oMRDataStatus).State = EntityState.Modified;
            }
            else
            {
                if (id != oMRDataStatus.StatusId)
                {
                    return BadRequest();
                }
                _secondDbContext.Entry(oMRDataStatus).State = EntityState.Modified;
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
                if (!OMRDataStatusExists(id, WhichDatabase))
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
                //_logger.LogEvent($"Deleted BookletPDFData in PaperID: {paperID}", "BookletPdfData", userId);
                string oMRDataStatusJson = JsonConvert.SerializeObject(oMRDataStatus);
                _changeLogger.LogForDBSync("Update", "OMRDataStatuss", oMRDataStatusJson, WhichDatabase, userId);
            }

            return NoContent();
        }

        [HttpPost]
        public async Task<ActionResult<OMRDataStatus>> PostOMRDataStatus(OMRDataStatus oMRDataStatus, string WhichDatabase)
        {
            if (WhichDatabase == "Local")
            {
                _firstDbContext.OMRDataStatuss.Add(oMRDataStatus);
            }
            else
            {
                _secondDbContext.OMRDataStatuss.Add(oMRDataStatus);
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
                if (OMRDataStatusExists(oMRDataStatus.StatusId, WhichDatabase))
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
                //_logger.LogEvent($"Deleted BookletPDFData in PaperID: {paperID}", "BookletPdfData", userId);
                string oMRDataStatusJson = JsonConvert.SerializeObject(oMRDataStatus);
                _changeLogger.LogForDBSync("Insert", "OMRDataStatuss", oMRDataStatusJson, WhichDatabase, userId);
            }
            return CreatedAtAction("GetOMRDataStatus", new { id = oMRDataStatus.StatusId }, oMRDataStatus);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteOMRDataStatus(int id, string WhichDatabase)
        {
            if (WhichDatabase == "Local")
            {
                var oMRDataStatus = await _firstDbContext.OMRDataStatuss.FindAsync(id);

                if (oMRDataStatus == null)
                {
                    return NotFound();

                }
                _firstDbContext.OMRDataStatuss.Remove(oMRDataStatus);
                await _firstDbContext.SaveChangesAsync();
            }
            else
            {
                var oMRDataStatus = await _firstDbContext.OMRDataStatuss.FindAsync(id);

                if (oMRDataStatus == null)
                {
                    return NotFound();

                }

                _secondDbContext.OMRDataStatuss.Remove(oMRDataStatus);
                await _secondDbContext.SaveChangesAsync();
            }
            return NoContent();
        }

        private bool OMRDataStatusExists(int id, string WhichDatabase)
        {
            if (WhichDatabase == "Local")
            {
                return _firstDbContext.OMRDataStatuss.Any(e => e.StatusId == id);
            }
            else
            {
                return _secondDbContext.OMRDataStatuss.Any(e => e.StatusId == id);
            }

        }
    }
}
