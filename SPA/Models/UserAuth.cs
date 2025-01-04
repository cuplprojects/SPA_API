using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SPA.Models
{
    public class UserAuth
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int UserAuthId { get; set; }
        public int UserId { get; set; }
        public bool AutogenPass { get; set; }
        public string Password { get; set; }
    }
}
