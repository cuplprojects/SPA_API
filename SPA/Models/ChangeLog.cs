using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System;

namespace SPA.Models
{
    public class ChangeLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public string Category { get; set; }

        public string Table {  get; set; }

        public string LogEntry {  get; set; }

        public DateTime LoggedAT { get; set; } = DateTime.UtcNow.AddMinutes(330); 

        public int UpdatedBy { get; set; }

        public bool IsSynced { get; set; } = false;

    }
}