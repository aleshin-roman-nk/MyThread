using JChrono.Domain;

namespace JChrono.Application.Features.Sessions;

public interface IDaySessionsQuery
{
    Task<IReadOnlyList<Session>> List(DateTimeOffset startTime, DateTimeOffset endTime, CancellationToken ct);
}

public sealed class GetDaySessions(IDaySessionsQuery query)
{
    public async Task<IReadOnlyList<DaySessionDto>> Execute(
        DateTimeOffset? startTime, DateTimeOffset? endTime, CancellationToken ct)
    {
        var (start, end) = SessionTimeRange.Validate(startTime, endTime);
        return SessionResults.ToDaySessions(await query.List(start, end, ct));
    }
}
