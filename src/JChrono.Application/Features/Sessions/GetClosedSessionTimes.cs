namespace JChrono.Application.Features.Sessions;

public sealed class GetClosedSessionTimes(ClosedSessionIntervals intervals)
{
    public Task<IReadOnlyList<ClosedSessionTimeDto>> Execute(
        DateTimeOffset? startTime, DateTimeOffset? endTime, CancellationToken ct) =>
        intervals.Read(startTime, endTime, ct);
}
