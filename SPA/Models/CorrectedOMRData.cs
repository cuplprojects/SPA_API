using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
namespace SPA
{
    public class CorrectedOMRData
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int CorrectedId { get; set; }
        public string CorrectedOmrData { get; set; }
        public int ProjectId { get; set; }
        public string OmrData { get; set; }
        public string BarCode { get; set; }

    }
}
