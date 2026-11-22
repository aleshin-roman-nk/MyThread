using JChrono.Domain;

namespace JChrono.Application.Features.Sessions;

public interface IDeleteSessionStore
{
    Task<Session?> FindSession(int id, CancellationToken ct);
    void RemoveSession(Session session);
    Task SaveChanges(CancellationToken ct);
}

public sealed class DeleteSession(IDeleteSessionStore store)
{
    public async Task Execute(int id, CancellationToken ct)
    {
        var session = await store.FindSession(id, ct) ?? throw new NotFoundException("Сессия не найдена.");
        store.RemoveSession(session);
        await store.SaveChanges(ct);
    }
}
