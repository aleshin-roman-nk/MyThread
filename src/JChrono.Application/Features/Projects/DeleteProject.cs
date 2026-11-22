namespace JChrono.Application.Features.Projects;

public interface IDeleteProjectStore
{
    Task Delete(int id, string confirmationCode, CancellationToken ct);
}

public sealed class DeleteProject(IDeleteProjectStore store)
{
    public Task Execute(int id, DeleteProjectRequest request, CancellationToken ct) =>
        store.Delete(id, request.ConfirmationCode, ct);
}
