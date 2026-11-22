using JChrono.Application.Features.Sessions;
using JChrono.Domain;
using Microsoft.EntityFrameworkCore;

namespace JChrono.Infrastructure.Features.Sessions;

public sealed class ClosedSessionTimesQuery(NotesDbContext db) : IClosedSessionTimesQuery
{
    public async Task<IReadOnlyList<Session>> ListOverlapping(
        DateTimeOffset startTime, DateTimeOffset endTime, CancellationToken ct)
    {
        var start = startTime.ToUniversalTime();
        var end = endTime.ToUniversalTime();
        return await db.Sessions.FromSqlInterpolated(
            $"SELECT * FROM Sessions WHERE EndTime IS NOT NULL AND StartTime < {end} AND EndTime > {start}")
            .AsNoTracking().ToListAsync(ct);
    }
}
