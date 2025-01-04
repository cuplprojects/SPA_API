using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using SPA.Models.NonDBModels;

namespace SPA
{
    public class ImageConfig
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public string ImageUrl { get; set; }
        public string AnnotationsJson { get; set; }

        [NotMapped]
        public List<Annotation> Annotations
        {
            get => AnnotationsJson == null ? null : JsonConvert.DeserializeObject<List<Annotation>>(AnnotationsJson);
            set => AnnotationsJson = JsonConvert.SerializeObject(value);
        }
        
    }
}
