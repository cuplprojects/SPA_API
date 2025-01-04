using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;
using SPA.Models.NonDBModels;
using Newtonsoft.Json;

namespace SPA
{
    public class ResponseConfig
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ResponseId { get; set; }

        public string? SectionsJson { get; set; }

        [NotMapped]
        public List<Section> Sections
        {
            get => SectionsJson == null ? new List<Section>() : JsonConvert.DeserializeObject<List<Section>>(SectionsJson);
            set => SectionsJson = JsonConvert.SerializeObject(value);
        }

        public string ResponseOption { get; set; }
        public int NumberOfBlocks { get; set; }
        public int? ProjectId { get; set; }
        public string CourseName { get; set; } = "";
    }
}
