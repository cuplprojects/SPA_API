using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace SPA.Models
{
    public class FlagAssignment
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AssignmentId { get; set; }

        public int UserId { get; set; }

        public int ProjectId { get; set; }

        public string FieldName { get; set; }

        public int StartFlagId { get; set; }

        public int EndFlagId { get; set; }

        public DateTime AssignedAt { get; set; } = DateTime.UtcNow.AddMinutes(330);

        public DateTime ExpiresAt { get; set; }
    }
}
