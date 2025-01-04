namespace SPA.Models
{
    public class ProjectArchive
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int ProjectId { get; set; }

        public DateTime ArchiveDate { get; set; }
    }
}
