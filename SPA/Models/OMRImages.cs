using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace SPA.Models
{
    public class OMRImage
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int OMRImagesID { get; set; }

        public string OMRImagesName { get; set; }

        public string FilePath {  get; set; }

        public int ProjectId { get; set; }
    }
}
