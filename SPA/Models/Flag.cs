using System.Text.Json;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace SPA
{
    public class Flag
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int FlagId { get; set; }
        public string Remarks { get; set; }
        public string FieldNameValue { get; set; }
        public string Field { get; set; }
        public string? BarCode { get; set; }
        public int ProjectId { get; set; }
        public bool isCorrected { get; set; } = false;
        public int? UpdatedByUserId { get; set; }
    }
}
