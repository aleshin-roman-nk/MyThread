using JChrono.Domain;
using Microsoft.EntityFrameworkCore;

namespace JChrono.Infrastructure;

public sealed class NotesDbContext(DbContextOptions<NotesDbContext> options) : DbContext(options)
{
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectTask> ProjectTasks => Set<ProjectTask>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Desktop> Desktops => Set<Desktop>();
    public DbSet<Sticker> Stickers => Set<Sticker>();
    public DbSet<PlannedActivity> PlannedActivities => Set<PlannedActivity>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<PlannedActivity>(entity =>
        {
            entity.Ignore(x => x.End);
            entity.Property(x => x.Description).HasMaxLength(PlannedActivity.MaxDescriptionLength).IsRequired();
            entity.HasOne(x => x.Subject).WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.SetNull);
            entity.HasMany(x => x.References).WithOne().HasForeignKey(x => x.ActivityId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.Date, x.StartTime, x.Id });
        });
        model.Entity<PlannedActivityReference>(entity =>
        {
            entity.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Task).WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.ActivityId, x.ProjectId }).IsUnique();
            entity.HasIndex(x => new { x.ActivityId, x.TaskId }).IsUnique();
            entity.ToTable("PlannedActivityReferences", table =>
                table.HasCheckConstraint("CK_PlannedActivityReferences_Target", "(ProjectId IS NULL) <> (TaskId IS NULL)"));
        });
        model.Entity<Desktop>().Property(x => x.Id).ValueGeneratedNever();
        model.Entity<Sticker>(entity =>
        {
            entity.HasOne<Desktop>().WithMany(x => x.Stickers).HasForeignKey(x => x.DesktopId);
            entity.HasOne(x => x.Task).WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Note).WithMany().HasForeignKey(x => x.NoteId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.DesktopId, x.TaskId }).IsUnique();
            entity.HasIndex(x => new { x.DesktopId, x.NoteId }).IsUnique();
            entity.Property(x => x.Text).HasMaxLength(Note.MaxTextLength);
            entity.ToTable(table => table.HasCheckConstraint("CK_Stickers_Content",
                "(Text IS NOT NULL) + (TaskId IS NOT NULL) + (NoteId IS NOT NULL) = 1"));
        });
        model.Entity<Subject>(entity =>
        {
            entity.Property(x => x.LastProjectNumber).HasDefaultValue(0L);
            entity.Property(x => x.Name).HasMaxLength(Subject.MaxNameLength).IsRequired();
            entity.Property(x => x.NormalizedName).HasMaxLength(Subject.MaxNameLength).IsRequired();
            entity.HasIndex(x => x.NormalizedName).IsUnique();
            entity.Property(x => x.ShortName).HasMaxLength(Subject.MaxShortNameLength).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(Subject.MaxDescriptionLength).IsRequired();
        });
        model.Entity<Project>(entity =>
        {
            entity.Property(x => x.LastTaskNumber).HasDefaultValue(0L);
            entity.Property(x => x.Name).HasMaxLength(Project.MaxNameLength).IsRequired();
            entity.Property(x => x.Code).HasMaxLength(Project.MaxCodeLength).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(Project.MaxDescriptionLength).IsRequired();
            entity.Property(x => x.NormalizedCode).HasMaxLength(Project.MaxCodeLength).IsRequired();
            entity.Property(x => x.Status).HasDefaultValue(ProjectStatus.InProgress).IsRequired();
            entity.Ignore(x => x.FullCode);
            entity.HasOne(x => x.Subject).WithMany(x => x.Projects).HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.SubjectId, x.NormalizedCode }).IsUnique();
        });
        model.Entity<ProjectTask>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(Project.MaxNameLength).IsRequired();
            entity.Property(x => x.Code).HasMaxLength(ProjectCode.MaxLength).IsRequired();
            entity.Property(x => x.NormalizedCode).HasMaxLength(ProjectCode.MaxLength).IsRequired();
            entity.Property(x => x.Body).HasMaxLength(ProjectTask.MaxBodyLength).IsRequired();
            entity.Property(x => x.Status).HasDefaultValue(ProjectTaskStatus.InProgress).IsRequired();
            entity.Ignore(x => x.FullCode);
            entity.HasOne(x => x.Project).WithMany(x => x.Tasks).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.ProjectId, x.NormalizedCode }).IsUnique();
        });
        model.Entity<Session>(entity =>
        {
            entity.Property(x => x.Text).HasMaxLength(Session.MaxTextLength).IsRequired();
            entity.Ignore(x => x.Duration);
            entity.Ignore(x => x.Status);
            entity.Property(x => x.StartTime).IsConcurrencyToken();
            entity.Property(x => x.EndTime).IsConcurrencyToken();
            entity.Property(x => x.Text).IsConcurrencyToken();
            // The unique expression index for EndTime IS NULL is created by DatabaseInitializer.
            entity.HasOne(x => x.Subject).WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Task).WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.SubjectId, x.StartTime });
        });
        model.Entity<Note>(entity =>
        {
            entity.Property(x => x.Text).HasMaxLength(Note.MaxTextLength).IsRequired();
            entity.HasOne(x => x.Subject).WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Task).WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.Date, x.Time, x.Id });
            entity.HasIndex(x => new { x.SubjectId, x.Date, x.Time, x.Id });
            entity.HasIndex(x => new { x.ProjectId, x.Date, x.Time, x.Id });
            entity.HasIndex(x => new { x.TaskId, x.Date, x.Time, x.Id });
            entity.ToTable("Note", table =>
            {
                table.HasCheckConstraint("CK_Note_Rating", "Rating BETWEEN 1 AND 5");
                table.HasCheckConstraint("CK_Note_Owner",
                    "(SubjectId IS NOT NULL) + (ProjectId IS NOT NULL) + (TaskId IS NOT NULL) <= 1");
            });
        });
    }
}

