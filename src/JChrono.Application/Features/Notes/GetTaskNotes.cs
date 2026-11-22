namespace JChrono.Application.Features.Notes;

public interface ITaskNotesQuery
{
    Task<bool> TaskExistsInProject(int projectId, int taskId, CancellationToken ct);
    Task<IReadOnlyList<TaskNoteDto>> ListByTask(int taskId, CancellationToken ct);
}

public sealed class GetTaskNotes(ITaskNotesQuery query)
{
    public async Task<IReadOnlyList<TaskNoteDto>> Execute(int projectId, int taskId, CancellationToken ct)
    {
        if (!await query.TaskExistsInProject(projectId, taskId, ct))
            throw new NotFoundException("Задача не найдена.");

        return await query.ListByTask(taskId, ct);
    }
}
