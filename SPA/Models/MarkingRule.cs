using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace SPA.Models
{
    public class MarkingRule
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int MarkingId { get; set; }
        public string MarkingName { get; set; }
    }
}
