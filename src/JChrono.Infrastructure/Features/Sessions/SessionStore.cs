using JChrono.Application.Features.Sessions;
using JChrono.Domain;
using Microsoft.EntityFrameworkCore;

namespace JChrono.Infrastructure.Features.Sessions;

public sealed class SessionStore(NotesDbContext db)
    : ICreateSessionStore, ICloseSessionStore, IUpdateSessionStore, IUpdateSessionTextStore,
        IDeleteSessionStore, ICurrentSessionQuery, ISessionQuery
{
    public Task<Session?> FindSession(int id, CancellationToken ct) =>
        db.Sessions.SingleOrDefaultAsync(session => session.Id == id, ct);

    public Task<Session?> FindCurrentSession(CancellationToken ct) =>
        db.Sessions.SingleOrDefaultAsync(session => session.EndTime == null, ct);

    public Task<Subject?> FindSubject(int id, CancellationToken ct) => db.FindTrackedSubject(id, ct);

    public Task<Project?> FindProject(int id, CancellationToken ct) => db.FindTrackedProject(id, ct);

    public Task<ProjectTask?> FindSessionTask(int id, CancellationToken ct) =>
        db.ProjectTasksWithContext().SingleOrDefaultAsync(task => task.Id == id, ct);

    public async Task AddSession(Session session, CancellationToken ct) => await db.Sessions.AddAsync(session, ct);

    public void RemoveSession(Session session) => db.Sessions.Remove(session);

    public Task SaveChanges(CancellationToken ct) => db.SaveChangesWithConflicts(ct);
}
