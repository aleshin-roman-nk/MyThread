using JChrono.Application;
using JChrono.Application.Features.Subjects;
using JChrono.Domain;
using Microsoft.EntityFrameworkCore;

namespace JChrono.Infrastructure.Features.Subjects;

public sealed class SubjectQueries(NotesDbContext db) : ISubjectsQuery, ISubjectQuery
{
    public Task<IReadOnlyList<SubjectDto>> List(CancellationToken ct) => ReadSubjects(null, ct);

    public async Task<SubjectDto?> Find(int id, CancellationToken ct) =>
        (await ReadSubjects(id, ct)).SingleOrDefault();

    private async Task<IReadOnlyList<SubjectDto>> ReadSubjects(int? id, CancellationToken ct)
    {
        var subjects = await db.Subjects.AsNoTracking().Where(s => !id.HasValue || s.Id == id.Value).OrderBy(s => s.Name)
            .Select(s => new SubjectDto(s.Id, s.Name, db.Notes.Count(n => n.SubjectId == s.Id), s.ShortName, 0,
                db.Projects.Count(p => p.SubjectId == s.Id),
                db.ProjectTasks.Count(t => t.Project.SubjectId == s.Id && t.Status == ProjectTaskStatus.InProgress),
                db.ProjectTasks.Count(t => t.Project.SubjectId == s.Id && t.Status == ProjectTaskStatus.Deferred), s.Description))
            .ToListAsync(ct);
        // SQLite does not support subtraction of DateTimeOffset values. Read only
        // the interval fields and sum exact durations in .NET, including multi-day sessions.
        var intervals = await db.Sessions.AsNoTracking()
            .Where(s => s.EndTime != null && (!id.HasValue || s.SubjectId == id.Value))
            .Select(s => new { s.SubjectId, s.StartTime, s.EndTime }).ToListAsync(ct);
        var hours = intervals.GroupBy(s => s.SubjectId)
            .ToDictionary(group => group.Key, group => group.Sum(s => (s.EndTime!.Value - s.StartTime).TotalHours));
        return subjects.Select(s => s with { ClosedSessionHours = hours.GetValueOrDefault(s.Id) }).ToList();
    }
}
