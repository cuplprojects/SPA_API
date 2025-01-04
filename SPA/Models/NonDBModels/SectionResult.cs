namespace SPA.Models.NonDBModels
{
    public class SectionResult
    {
        public string SectionName { get; set; }
        public int TotalCorrectAnswers { get; set; }
        public int TotalWrongAnswers { get; set; }
        public double TotalScoreSub { get; set; }
    }
}