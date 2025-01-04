using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace SPA
{
    public class Absentee
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AbsenteeId { get; set; }
        public string DistrictCode { get; set; }
        public string CenterCode { get; set; }
        public string RollNo { get; set; }
        public int ProjectID { get; set; }
    }
}
