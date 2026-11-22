using JChrono.Application.Features.Sessions;
using JChrono.Domain;
using Microsoft.EntityFrameworkCore;

namespace JChrono.Infrastructure.Features.Sessions;

public sealed class SessionListQueries(NotesDbContext db) : ISubjectSessionsQuery, IProjectSessionsQuery, ITaskSessionsQuery
{
    public Task<bool> SubjectExists(int subjectId, CancellationToken ct) =>
        db.Subjects.AnyAsync(subject => subject.Id == subjectId, ct);

    public async Task<IReadOnlyList<Session>> ListBySubject(int subjectId, CancellationToken ct) =>
        await db.Sessions.AsNoTracking().Where(session => session.SubjectId == subjectId).ToListAsync(ct);

    public Task<bool> ProjectExists(int projectId, CancellationToken ct) =>
        db.Projects.AnyAsync(project => project.Id == projectId, ct);

    public async Task<IReadOnlyList<Session>> ListByProject(int projectId, CancellationToken ct) =>
        await db.Sessions.AsNoTracking().Where(session => session.ProjectId == projectId).ToListAsync(ct);

    public Task<bool> TaskExistsInProject(int projectId, int taskId, CancellationToken ct) =>
        db.ProjectTasks.AnyAsync(task => task.Id == taskId && task.ProjectId == projectId, ct);

    public async Task<IReadOnlyList<Session>> ListByTask(int taskId, CancellationToken ct) =>
        await db.Sessions.AsNoTracking().Where(session => session.TaskId == taskId).ToListAsync(ct);
}
