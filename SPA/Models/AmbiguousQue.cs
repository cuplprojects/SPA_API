using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
namespace SPA.Models
{
    public class AmbiguousQue
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AmbiguousId { get; set; }
        public int ProjectId { get; set; }
        public int MarkingId { get; set; }
        [MaxLength(1)]
        public string SetCode { get; set; }
        public int QuestionNumber { get; set; }
        public string? Option { get; set; }
        [MaxLength(50)]
        public string? Course { get; set; }
    }
}
