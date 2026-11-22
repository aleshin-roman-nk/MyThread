using JChrono.Application.Features.Desktop;
using JChrono.Domain;
using Microsoft.EntityFrameworkCore;

namespace JChrono.Infrastructure.Features.Desktop;

public sealed class TaskPinStore(NotesDbContext db) : ITaskPinStatusQuery, IPinTaskStore, IUnpinTaskStore
{
    public Task<bool> IsTaskPinned(int taskId, CancellationToken ct) =>
        db.DefaultDesktopStickers().AnyAsync(sticker => sticker.TaskId == taskId, ct);

    public async Task<Sticker?> PinTask(int taskId, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO Stickers (DesktopId, TaskId)
            SELECT {JChrono.Domain.Desktop.DefaultId}, Id FROM ProjectTasks WHERE Id = {taskId}
            ON CONFLICT (DesktopId, TaskId) DO NOTHING;
            """, ct);
        return await db.DefaultDesktopStickers().WithStickerContent().AsNoTracking()
            .SingleOrDefaultAsync(sticker => sticker.TaskId == taskId, ct);
    }

    public async Task UnpinTask(int taskId, CancellationToken ct) =>
        await db.DefaultDesktopStickers().Where(sticker => sticker.TaskId == taskId).ExecuteDeleteAsync(ct);
}
