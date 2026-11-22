using JChrono.Application.Features.Subjects;
using JChrono.Domain;
using Microsoft.EntityFrameworkCore;

namespace JChrono.Infrastructure.Features.Subjects;

public sealed class SubjectStore(NotesDbContext db) : ICreateSubjectStore, IUpdateSubjectStore, ISetSubjectShortNameStore
{
    public Task<bool> SubjectNameExists(string normalizedName, CancellationToken ct, int? excludingId = null) =>
        db.Subjects.AnyAsync(subject => subject.NormalizedName == normalizedName
            && (!excludingId.HasValue || subject.Id != excludingId.Value), ct);

    public async Task AddSubject(Subject subject, CancellationToken ct) =>
        await db.Subjects.AddAsync(subject, ct);

    public Task<Subject?> FindSubject(int id, CancellationToken ct) => db.FindTrackedSubject(id, ct);

    public Task SaveChanges(CancellationToken ct) => db.SaveChangesWithConflicts(ct);
}
