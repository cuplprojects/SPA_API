namespace SPA.Models.NonDBModels
{
    public class Section
    {
        public string Name { get; set; }
        public int NumQuestions { get; set; }
        public int MarksCorrect { get; set; }
        public bool NegativeMarking { get; set; }
        public double MarksWrong { get; set; }
        public int TotalMarks { get; set; }
        public int StartQuestion { get; set; }
        public int EndQuestion { get; set; }
    }
}
