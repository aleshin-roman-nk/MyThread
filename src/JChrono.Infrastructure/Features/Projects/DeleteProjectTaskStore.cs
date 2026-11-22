using JChrono.Application;
using JChrono.Application.Features.Projects;
using Microsoft.EntityFrameworkCore;

namespace JChrono.Infrastructure.Features.Projects;

public sealed class DeleteProjectTaskStore(NotesDbContext db) : IDeleteProjectTaskStore
{
    public async Task Delete(int projectId, int id, string confirmationCode, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var task = await db.ProjectTasks.AsNoTracking().SingleOrDefaultAsync(t => t.ProjectId == projectId && t.Id == id, ct)
            ?? throw new NotFoundException("Задача не найдена.");
        if (!string.Equals(confirmationCode, task.Code, StringComparison.Ordinal))
            throw new ArgumentException("Для удаления введите точный код задачи.");

        await db.Sessions.Where(s => s.TaskId == id).ExecuteDeleteAsync(ct);
        await db.Notes.Where(n => n.TaskId == id).ExecuteDeleteAsync(ct);
        await db.ProjectTasks.Where(t => t.Id == id && t.ProjectId == projectId).ExecuteDeleteAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
