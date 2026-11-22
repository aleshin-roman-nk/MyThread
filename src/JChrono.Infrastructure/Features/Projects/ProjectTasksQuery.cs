using JChrono.Application.Features.Projects;
using JChrono.Domain;
using Microsoft.EntityFrameworkCore;

namespace JChrono.Infrastructure.Features.Projects;

public sealed class ProjectTasksQuery(NotesDbContext db) : IProjectTasksQuery
{
    public Task<Project?> FindProjectWithTasks(int projectId, ProjectTaskStatus? status, CancellationToken ct) =>
        db.Projects.AsNoTracking()
            .Include(project => project.Subject)
            .Include(project => project.Tasks.Where(task => !status.HasValue || task.Status == status.Value))
            .SingleOrDefaultAsync(project => project.Id == projectId, ct);
}
