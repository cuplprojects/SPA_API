namespace SPA.Models.NonDBModels
{

    public class Sets
    {
        public string Set { get; set; }
        public List<Question> Questions { get; set; }
    }

    public class Question
    {
        public string QuestionNo { get; set; }
        public string Answer { get; set; }
    }
}

