using JChrono.Domain;

namespace JChrono.Application.Features.Sessions;

public interface IClosedSessionTimesQuery
{
    Task<IReadOnlyList<Session>> ListOverlapping(DateTimeOffset startTime, DateTimeOffset endTime, CancellationToken ct);
}

public sealed class ClosedSessionIntervals(IClosedSessionTimesQuery query)
{
    public async Task<IReadOnlyList<ClosedSessionTimeDto>> Read(
        DateTimeOffset? startTime, DateTimeOffset? endTime, CancellationToken ct)
    {
        var (start, end) = SessionTimeRange.Validate(startTime, endTime);
        var sessions = await query.ListOverlapping(start, end, ct);
        return sessions.Select(session => new ClosedSessionTimeDto(
            session.SubjectId,
            session.StartTime < start ? start : session.StartTime,
            session.EndTime!.Value > end ? end : session.EndTime.Value)).ToList();
    }
}
