using JChrono.Domain;

namespace JChrono.Application.Features.Sessions;

public interface IProjectSessionsQuery
{
    Task<bool> ProjectExists(int projectId, CancellationToken ct);
    Task<IReadOnlyList<Session>> ListByProject(int projectId, CancellationToken ct);
}

public sealed class GetProjectSessions(IProjectSessionsQuery query)
{
    public async Task<IReadOnlyList<SessionDto>> Execute(int projectId, CancellationToken ct)
    {
        if (!await query.ProjectExists(projectId, ct))
            throw new NotFoundException("Проект не найден.");

        return SessionResults.ToSessions(await query.ListByProject(projectId, ct));
    }
}
