using JChrono.Domain;

namespace JChrono.Application.Features.Sessions;

public interface ISubjectJournalQuery
{
    Task<bool> SubjectExists(int subjectId, CancellationToken ct);
    Task<IReadOnlyList<Session>> ListOverlapping(int subjectId, DateTimeOffset startTime, DateTimeOffset endTime, CancellationToken ct);
}

public sealed class GetSubjectJournal(ISubjectJournalQuery query, TimeProvider clock)
{
    public async Task<IReadOnlyList<DaySessionDto>> Execute(
        int subjectId, DateTimeOffset? startTime, DateTimeOffset? endTime, CancellationToken ct)
    {
        var (start, end) = SessionTimeRange.Validate(startTime, endTime);
        if (!await query.SubjectExists(subjectId, ct))
            throw new NotFoundException("Предмет не найден.");

        var sessions = await query.ListOverlapping(subjectId, start, end, ct);
        var now = clock.GetUtcNow();
        return SessionResults.ToDaySessions(sessions.Where(session =>
            session.EndTime.HasValue || (now > start && now >= session.StartTime)));
    }
}
