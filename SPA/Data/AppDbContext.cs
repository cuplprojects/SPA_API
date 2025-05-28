using Microsoft.EntityFrameworkCore;
using MySqlX.XDevAPI;
using SPA.Encryptions;
using SPA;
using SPA.Models;
using Org.BouncyCastle.Math.EC;

namespace SPA.Data
{
    public class FirstDbContext : DbContext
    {
        public FirstDbContext(DbContextOptions<FirstDbContext> options)
            : base(options)
        {
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Absentee>()
                .HasIndex(p => new { p.RollNo, p.ProjectID })
                .IsUnique();
            modelBuilder.Entity<Field>()
                .HasIndex(p => p.FieldName)
                .IsUnique();
            modelBuilder.Entity<OMRImage>()
                .HasIndex(p => new { p.OMRImagesName, p.ProjectId })
                .IsUnique();
            modelBuilder.Entity<OMRdata>()
                .HasIndex(p => new {p.BarCode, p.ProjectId})
                .IsUnique();

        }
        public DbSet<User> Users { get; set; }
        public DbSet<UserAuth> UserAuths { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Module> Modules { get; set; }
        public DbSet<Score> Scores { get; set; }
        public DbSet<Report> Reports { get; set; }
        public DbSet<ResponseConfig> ResponseConfigs { get; set; }
        public DbSet<RegistrationData> RegistrationDatas { get; set; }
        public DbSet<Project> Projects { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<OrganizationPlan> OrganizationPlans { get; set; }
        public DbSet<OMRdata> OMRdatas { get; set; }
        public DbSet<ExtractedOMRData> ExtractedOMRDatas { get; set; }
        public DbSet<CorrectedOMRData> CorrectedOMRDatas { get; set; }
        public DbSet<Keys> Keyss { get; set; }
        public DbSet<ImageConfig> ImageConfigs { get; set; }
        public DbSet<Absentee> Absentees { get; set; }
        public DbSet<AmbiguousQue> AmbiguousQues { get; set; }
        public DbSet<Field> Fields { get; set; }
        public DbSet<Flag> Flags { get; set; }
        public DbSet<FieldConfig> FieldConfigs { get; set; }
        public DbSet<MarkingRule> MarkingRules { get; set; }
        public DbSet<ChangeLog> ChangeLogs { get; set; }
        public DbSet<OMRImage> OMRImages { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<ProjectArchive> ProjectArchives { get; set; }
        public DbSet<FlagAssignment> FlagAssignments { get; set; }
        public DbSet<OMRDataStatus> OMRDataStatuss { get; set; }
        public DbSet<EventLog> EventLogs { get; set; }
        public DbSet<ErrorLog> ErrorLogs { get; set; }
    }

    public class SecondDbContext : DbContext
    {
        public SecondDbContext(DbContextOptions<SecondDbContext> options)
            : base(options)
        {
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Absentee>()
                .HasIndex(p => new { p.RollNo, p.ProjectID })
                .IsUnique();
            modelBuilder.Entity<Field>()
                .HasIndex(p => p.FieldName)
                .IsUnique();
            modelBuilder.Entity<OMRImage>()
                .HasIndex(p => new { p.OMRImagesName, p.ProjectId })
                .IsUnique();
            modelBuilder.Entity<OMRdata>()
                .HasIndex(p => new { p.BarCode, p.ProjectId })
                .IsUnique();

        }
        public DbSet<User> Users { get; set; }
        public DbSet<UserAuth> UserAuths { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Module> Modules { get; set; }
        public DbSet<Score> Scores { get; set; }
        public DbSet<Report> Reports { get; set; }
        public DbSet<ResponseConfig> ResponseConfigs { get; set; }
        public DbSet<RegistrationData> RegistrationDatas { get; set; }
        public DbSet<Project> Projects { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<OrganizationPlan> OrganizationPlans { get; set; }
        public DbSet<OMRdata> OMRdatas { get; set; }
        public DbSet<ExtractedOMRData> ExtractedOMRDatas { get; set; }
        public DbSet<CorrectedOMRData> CorrectedOMRDatas { get; set; }
        public DbSet<Keys> Keyss { get; set; }
        public DbSet<ImageConfig> ImageConfigs { get; set; }
        public DbSet<Absentee> Absentees { get; set; }
        public DbSet<AmbiguousQue> AmbiguousQues { get; set; }
        public DbSet<Field> Fields { get; set; }
        public DbSet<Flag> Flags { get; set; }
        public DbSet<FieldConfig> FieldConfigs { get; set; }
        public DbSet<MarkingRule> MarkingRules { get; set; }
        public DbSet<ChangeLog> ChangeLogs { get; set; }
        public DbSet<OMRImage> OMRImages { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<ProjectArchive> ProjectArchives { get; set; }
        public DbSet<FlagAssignment> FlagAssignments { get; set; }
        public DbSet<OMRDataStatus> OMRDataStatuss { get; set; }
        public DbSet<EventLog> EventLogs { get; set; }
        public DbSet<ErrorLog> ErrorLogs { get; set; }
    }

}

