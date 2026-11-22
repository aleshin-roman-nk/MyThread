using JChrono.Domain;

namespace JChrono.Application.Features.Sessions;

public interface ISubjectSessionsQuery
{
    Task<bool> SubjectExists(int subjectId, CancellationToken ct);
    Task<IReadOnlyList<Session>> ListBySubject(int subjectId, CancellationToken ct);
}

public sealed class GetSubjectSessions(ISubjectSessionsQuery query)
{
    public async Task<IReadOnlyList<SessionDto>> Execute(int subjectId, CancellationToken ct)
    {
        if (!await query.SubjectExists(subjectId, ct))
            throw new NotFoundException("Предмет не найден.");

        return SessionResults.ToSessions(await query.ListBySubject(subjectId, ct));
    }
}
