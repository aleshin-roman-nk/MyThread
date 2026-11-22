namespace JChrono.Application.Features.Desktop;

public interface IUnpinTaskStore
{
    Task UnpinTask(int taskId, CancellationToken ct);
}

public sealed class UnpinTask(IUnpinTaskStore store)
{
    public Task Execute(int taskId, CancellationToken ct) => store.UnpinTask(taskId, ct);
}
