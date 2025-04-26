using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SPA.Models
{
    public class OrganizationPlan
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int TenantId { get; set; }
        public string OrganizationName { get; set; }
        public string PlanName { get; set; }
        public DateTime StartedDate { get; set; }
        public DateTime EndDate { get; set; }
    }
}
