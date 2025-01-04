using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SPA
{
    public class Role
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int RoleId { get; set; }

        [Required]
        public string RoleName { get; set; }

        public bool IsActive { get; set; }

        public string Permission { get; set; }

        [NotMapped]
        public List<string> PermissionList
        {
            get => string.IsNullOrEmpty(Permission) ? new List<string>() : JsonConvert.DeserializeObject<List<string>>(Permission);
            set => Permission = JsonConvert.SerializeObject(value);
        }

    }
}
