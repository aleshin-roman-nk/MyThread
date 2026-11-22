using JChrono.Domain;

namespace JChrono.Application.Features.Sessions;

public interface ISessionQuery
{
    Task<Session?> FindSession(int id, CancellationToken ct);
}

public sealed class GetSession(ISessionQuery query)
{
    public async Task<SessionDto> Execute(int id, CancellationToken ct)
    {
        var session = await query.FindSession(id, ct) ?? throw new NotFoundException("Сессия не найдена.");
        return SessionResults.ToDto(session);
    }
}
