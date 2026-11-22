using JChrono.Domain;

namespace JChrono.Application.Features.Sessions;

public interface ITaskSessionsQuery
{
    Task<bool> TaskExistsInProject(int projectId, int taskId, CancellationToken ct);
    Task<IReadOnlyList<Session>> ListByTask(int taskId, CancellationToken ct);
}

public sealed class GetTaskSessions(ITaskSessionsQuery query)
{
    public async Task<IReadOnlyList<SessionDto>> Execute(int projectId, int taskId, CancellationToken ct)
    {
        if (!await query.TaskExistsInProject(projectId, taskId, ct))
            throw new NotFoundException("Задача не найдена.");

        return SessionResults.ToSessions(await query.ListByTask(taskId, ct));
    }
}
