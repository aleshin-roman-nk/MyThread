using JChrono.Domain;

namespace JChrono.Application.Features.Sessions;

public interface ICurrentSessionQuery
{
    Task<Session?> FindCurrentSession(CancellationToken ct);
}

public sealed class GetCurrentSession(ICurrentSessionQuery query)
{
    public async Task<SessionDto?> Execute(CancellationToken ct) =>
        await query.FindCurrentSession(ct) is { } session ? SessionResults.ToDto(session) : null;
}
