using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace SPA.Models
{
    public class ExtractedOMRData
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ExtractedOmrDataId { get; set; }

        public string? ExtractedOmrData { get; set; }
        public string BarCode { get; set; }
        public int Status { get; set; }
        public int ProjectId { get; set; }
        public int AuditCycleNumber { get; set; } = 0;
    }
}
