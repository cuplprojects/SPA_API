using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SPA.Data;
using SPA.Models;

namespace SPA.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrganizationPlanController : ControllerBase
    {
        private readonly FirstDbContext _context;
        public OrganizationPlanController(FirstDbContext firstDbContext)
        {
            _context = firstDbContext;
}
            [HttpPost]
            public IActionResult CreateOrganizationPlan([FromBody] OrganizationPlan organizationPlan)
            {
                if (organizationPlan == null)
                {
                    return BadRequest("Invalid data.");
                }

                _context.OrganizationPlans.Add(organizationPlan);
                _context.SaveChanges();

                return CreatedAtAction("GetOrganization", new { id = organizationPlan.TenantId }, organizationPlan);
            
        }
    }
}
