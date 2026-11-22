using JChrono.Application;
using JChrono.Application.Features.Subjects;
using Microsoft.EntityFrameworkCore;

namespace JChrono.Infrastructure.Features.Subjects;

public sealed class DeleteSubjectStore(NotesDbContext db) : IDeleteSubjectStore
{
    public async Task Delete(int id, string confirmationShortName, CancellationToken ct)
    {
        // Keep validation and all deletes in one transaction, including on existing databases
        // whose foreign keys deliberately use RESTRICT.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var subject = await db.Subjects.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException("Предмет не найден.");
        if (!string.Equals(confirmationShortName, subject.ShortName, StringComparison.Ordinal))
            throw new ArgumentException("Для удаления введите точное короткое имя предмета.");

        await db.Sessions.Where(s => s.SubjectId == id).ExecuteDeleteAsync(ct);
        await db.Notes.Where(n => n.SubjectId == id || (n.Project != null && n.Project.SubjectId == id)
            || (n.Task != null && n.Task.Project.SubjectId == id)).ExecuteDeleteAsync(ct);
        await db.ProjectTasks.Where(t => t.Project.SubjectId == id).ExecuteDeleteAsync(ct);
        await db.Projects.Where(p => p.SubjectId == id).ExecuteDeleteAsync(ct);
        await db.Subjects.Where(s => s.Id == id).ExecuteDeleteAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
