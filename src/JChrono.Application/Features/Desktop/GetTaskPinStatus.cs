namespace JChrono.Application.Features.Desktop;

public interface ITaskPinStatusQuery
{
    Task<bool> IsTaskPinned(int taskId, CancellationToken ct);
}

public sealed class GetTaskPinStatus(ITaskPinStatusQuery query)
{
    public async Task<TaskPinStatusDto> Execute(int taskId, CancellationToken ct) =>
        new(await query.IsTaskPinned(taskId, ct));
}
