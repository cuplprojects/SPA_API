using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using Newtonsoft.Json;
using SPA.Models.NonDBModels;
using System.Reflection;
using System.Text.Json;

namespace SPA
{
    public class FieldConfig
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int FieldConfigurationId { get; set; }
        public int ProjectId { get; set; }
        public string FieldName { get; set; }
        public bool canBlank { get; set; }
        public string? FieldAttributesJson { get; set; }

        [NotMapped]
        public List<FieldAttribute> FieldAttributes
        {
            get => FieldAttributesJson == null ? null : JsonConvert.DeserializeObject<List<FieldAttribute>>(FieldAttributesJson);
            set => FieldAttributesJson = JsonConvert.SerializeObject(value);
        }

    }


}
