using JChrono.Domain;
using Microsoft.EntityFrameworkCore;

namespace JChrono.Infrastructure;

internal static class TrackedReferences
{
    public static Task<Subject?> FindTrackedSubject(this NotesDbContext db, int id, CancellationToken ct) =>
        db.Subjects.SingleOrDefaultAsync(subject => subject.Id == id, ct);

    public static Task<Project?> FindTrackedProject(this NotesDbContext db, int id, CancellationToken ct) =>
        db.Projects.Include(project => project.Subject).Include(project => project.Tasks)
            .SingleOrDefaultAsync(project => project.Id == id, ct);

    public static IQueryable<ProjectTask> ProjectTasksWithContext(this NotesDbContext db) =>
        db.ProjectTasks.Include(task => task.Project).ThenInclude(project => project.Subject);

    public static Task<ProjectTask?> FindTrackedProjectTask(this NotesDbContext db, int projectId, int id, CancellationToken ct) =>
        db.ProjectTasksWithContext().SingleOrDefaultAsync(task => task.Id == id && task.ProjectId == projectId, ct);
}
