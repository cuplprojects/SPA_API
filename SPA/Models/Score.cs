using Newtonsoft.Json;
using SPA.Models.NonDBModels;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
namespace SPA.Models
{
    public class Score
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]

        public int ScoreId { get; set; }
        public string ScoreData { get; set; }
        public string RollNumber { get; set; }
        public int ProjectId { get; set; }
        public string CourseName { get; set; }
        public double TotalScore { get; set; }

        [NotMapped]
        public List<SectionResult> SectionResult
        {
            get => ScoreData == null ? new List<SectionResult>() : JsonConvert.DeserializeObject<List<SectionResult>>(ScoreData);

        }
    }
}
