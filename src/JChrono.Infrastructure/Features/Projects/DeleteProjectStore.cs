using JChrono.Application;
using JChrono.Application.Features.Projects;
using Microsoft.EntityFrameworkCore;

namespace JChrono.Infrastructure.Features.Projects;

public sealed class DeleteProjectStore(NotesDbContext db) : IDeleteProjectStore
{
    public async Task Delete(int id, string confirmationCode, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var project = await db.Projects.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Проект не найден.");
        if (!string.Equals(confirmationCode, project.Code, StringComparison.Ordinal))
            throw new ArgumentException("Для удаления введите точный код проекта.");

        await db.Sessions.Where(s => s.ProjectId == id || (s.Task != null && s.Task.ProjectId == id)).ExecuteDeleteAsync(ct);
        await db.Notes.Where(n => n.ProjectId == id || (n.Task != null && n.Task.ProjectId == id)).ExecuteDeleteAsync(ct);
        await db.ProjectTasks.Where(t => t.ProjectId == id).ExecuteDeleteAsync(ct);
        await db.Projects.Where(p => p.Id == id).ExecuteDeleteAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
