namespace JChrono.Application.Features.Projects;

public interface IDeleteProjectTaskStore
{
    Task Delete(int projectId, int id, string confirmationCode, CancellationToken ct);
}

public sealed class DeleteProjectTask(IDeleteProjectTaskStore store)
{
    public Task Execute(int projectId, int id, DeleteTaskRequest request, CancellationToken ct) =>
        store.Delete(projectId, id, request.ConfirmationCode, ct);
}
