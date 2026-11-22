using JChrono.Domain;

namespace JChrono.Application.Features.Sessions;

public interface IUpdateSessionTextStore
{
    Task<Session?> FindSession(int id, CancellationToken ct);
    Task SaveChanges(CancellationToken ct);
}

public sealed class UpdateSessionText(IUpdateSessionTextStore store)
{
    public async Task<SessionDto> Execute(int id, UpdateSessionTextRequest request, CancellationToken ct)
    {
        var session = await store.FindSession(id, ct) ?? throw new NotFoundException("Сессия не найдена.");
        session.UpdateText(request.Text);
        await store.SaveChanges(ct);
        return SessionResults.ToDto(session);
    }
}
