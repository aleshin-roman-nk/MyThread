using JChrono.Application.Features.Sessions;
using JChrono.Domain;
using Microsoft.EntityFrameworkCore;

namespace JChrono.Infrastructure.Features.Sessions;

public sealed class SubjectJournalQuery(NotesDbContext db) : ISubjectJournalQuery
{
    public Task<bool> SubjectExists(int subjectId, CancellationToken ct) =>
        db.Subjects.AnyAsync(subject => subject.Id == subjectId, ct);

    public async Task<IReadOnlyList<Session>> ListOverlapping(
        int subjectId, DateTimeOffset startTime, DateTimeOffset endTime, CancellationToken ct)
    {
        var start = startTime.ToUniversalTime();
        var end = endTime.ToUniversalTime();
        return await db.Sessions.FromSqlInterpolated($"""
            SELECT * FROM Sessions WHERE SubjectId = {subjectId} AND StartTime < {end}
                AND (StartTime >= {start} OR EndTime IS NULL OR EndTime > {start})
            """).WithSessionContext().ToListAsync(ct);
    }
}
