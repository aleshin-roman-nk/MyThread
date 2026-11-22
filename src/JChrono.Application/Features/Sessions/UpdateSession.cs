using JChrono.Domain;

namespace JChrono.Application.Features.Sessions;

public interface IUpdateSessionStore
{
    Task<Session?> FindSession(int id, CancellationToken ct);
    Task<Session?> FindCurrentSession(CancellationToken ct);
    Task SaveChanges(CancellationToken ct);
}

public sealed class UpdateSession(IUpdateSessionStore store)
{
    public async Task<SessionDto> Execute(int id, UpdateSessionRequest request, CancellationToken ct)
    {
        var session = await store.FindSession(id, ct) ?? throw new NotFoundException("Сессия не найдена.");
        if (!request.StartTime.HasValue)
            throw new ArgumentException("Укажите время начала сессии.");
        if (!request.EndTime.HasValue && await store.FindCurrentSession(ct) is { } current && current.Id != id)
            throw new ConflictException("Уже есть открытая сессия. Сначала закройте её.");

        session.Update(request.StartTime.Value, request.EndTime, request.Text);
        await store.SaveChanges(ct);
        return SessionResults.ToDto(session);
    }
}
