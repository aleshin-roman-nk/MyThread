namespace JChrono.Application.Features.Subjects;

public interface IDeleteSubjectStore
{
    Task Delete(int id, string confirmationShortName, CancellationToken ct);
}

public sealed class DeleteSubject(IDeleteSubjectStore store)
{
    public Task Execute(int id, DeleteSubjectRequest request, CancellationToken ct) =>
        store.Delete(id, request.ConfirmationShortName, ct);
}
