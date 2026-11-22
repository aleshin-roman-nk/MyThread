using JChrono.Application.Features.Projects;
using JChrono.Domain;
using Microsoft.EntityFrameworkCore;

namespace JChrono.Infrastructure.Features.Projects;

public sealed class ProjectQueries(NotesDbContext db) : IProjectsQuery, IProjectQuery
{
    public Task<bool> SubjectExists(int subjectId, CancellationToken ct) =>
        db.Subjects.AnyAsync(subject => subject.Id == subjectId, ct);

    public async Task<IReadOnlyList<Project>> List(int subjectId, ProjectStatus? status, CancellationToken ct) =>
        await ReadProjects()
            .Where(p => p.SubjectId == subjectId && (!status.HasValue || p.Status == status.Value))
            .OrderBy(p => p.Name).ThenBy(p => p.Id).ToListAsync(ct);

    public Task<Project?> Find(int id, CancellationToken ct) =>
        ReadProjects().SingleOrDefaultAsync(p => p.Id == id, ct);

    private IQueryable<Project> ReadProjects() =>
        db.Projects.AsNoTracking().Include(p => p.Subject).Include(p => p.Tasks);
}
