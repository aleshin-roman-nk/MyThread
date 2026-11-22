using JChrono.Application.Features.Sessions;
using JChrono.Domain;
using Microsoft.EntityFrameworkCore;

namespace JChrono.Infrastructure.Features.Sessions;

public sealed class DaySessionsQuery(NotesDbContext db) : IDaySessionsQuery
{
    public async Task<IReadOnlyList<Session>> List(DateTimeOffset startTime, DateTimeOffset endTime, CancellationToken ct)
    {
        // All stored session times and these bounds use UTC. SQLite can compare their
        // canonical text values; EF's DateTimeOffset range comparison is unsupported.
        var start = startTime.ToUniversalTime();
        var end = endTime.ToUniversalTime();
        return await db.Sessions.FromSqlInterpolated(
            $"SELECT * FROM Sessions WHERE StartTime >= {start} AND StartTime < {end}")
            .WithSessionContext().ToListAsync(ct);
    }
}
