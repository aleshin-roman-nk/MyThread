using JChrono.Domain;

namespace JChrono.Application.Features.Sessions;

public interface ICreateSessionStore
{
    Task<Session?> FindCurrentSession(CancellationToken ct);
    Task<Subject?> FindSubject(int id, CancellationToken ct);
    Task<Project?> FindProject(int id, CancellationToken ct);
    Task<ProjectTask?> FindSessionTask(int id, CancellationToken ct);
    Task AddSession(Session session, CancellationToken ct);
    Task SaveChanges(CancellationToken ct);
}

public sealed class CreateSession(ICreateSessionStore store, TimeProvider clock)
{
    public async Task<SessionDto> Execute(CreateSessionRequest request, CancellationToken ct)
    {
        if (await store.FindCurrentSession(ct) is not null)
            throw new ConflictException("Уже есть открытая сессия. Сначала закройте её.");

        var subject = await store.FindSubject(request.SubjectId, ct) ?? throw new NotFoundException("Предмет не найден.");
        var project = request.ProjectId.HasValue
            ? await store.FindProject(request.ProjectId.Value, ct) ?? throw new NotFoundException("Проект не найден.") : null;
        var task = request.TaskId.HasValue
            ? await store.FindSessionTask(request.TaskId.Value, ct) ?? throw new NotFoundException("Задача не найдена.") : null;
        var session = new Session(subject, clock.GetUtcNow(), request.Text, project, task);
        await store.AddSession(session, ct);
        await store.SaveChanges(ct);
        return SessionResults.ToDto(session);
    }
}
