namespace JChrono.Application.Features.Sessions;

public sealed class GetUsefulWork(ClosedSessionIntervals intervals)
{
    public async Task<UsefulWorkDto> Execute(DateTimeOffset? startTime, DateTimeOffset? endTime, CancellationToken ct)
    {
        var sessions = await intervals.Read(startTime, endTime, ct);
        return new UsefulWorkDto(sessions.Sum(session => (session.EndTime - session.StartTime).TotalSeconds));
    }
}
