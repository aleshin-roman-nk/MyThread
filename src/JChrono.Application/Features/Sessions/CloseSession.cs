using JChrono.Domain;

namespace JChrono.Application.Features.Sessions;

public interface ICloseSessionStore
{
    Task<Session?> FindSession(int id, CancellationToken ct);
    Task SaveChanges(CancellationToken ct);
}

public sealed class CloseSession(ICloseSessionStore store, TimeProvider clock)
{
    public async Task<SessionDto> Execute(int id, CancellationToken ct)
    {
        var session = await store.FindSession(id, ct) ?? throw new NotFoundException("Сессия не найдена.");
        if (session.Status == SessionStatus.Closed)
            throw new ConflictException("Сессия уже закрыта.");

        session.Close(clock.GetUtcNow());
        await store.SaveChanges(ct);
        return SessionResults.ToDto(session);
    }
}
