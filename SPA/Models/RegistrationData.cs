using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace SPA
{
    public class RegistrationData
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int RegistrationId { get; set; }
        public string RegistrationsData { get; set; }
        public string RollNumber { get; set; }
        public int ProjectId { get; set; }
    }
}
