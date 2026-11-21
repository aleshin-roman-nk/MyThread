using DailyNotes.Application;
using DailyNotes.Domain;
using Microsoft.EntityFrameworkCore;

namespace DailyNotes.Infrastructure;

public sealed class DesktopRepository(NotesDbContext db) : IDesktopRepository
{
    private IQueryable<Sticker> DesktopStickers => db.Stickers.Where(sticker => sticker.DesktopId == Desktop.DefaultId);

    private IQueryable<Sticker> Stickers => DesktopStickers
        .Include(sticker => sticker.Task).ThenInclude(task => task!.Project).ThenInclude(project => project.Subject)
        .Include(sticker => sticker.Note).ThenInclude(note => note!.Subject)
        .Include(sticker => sticker.Note).ThenInclude(note => note!.Project)
        .Include(sticker => sticker.Note).ThenInclude(note => note!.Task);

    public async Task<IReadOnlyList<Sticker>> GetStickers(CancellationToken ct) =>
        await Stickers.AsNoTracking().OrderBy(sticker => sticker.Id).ToListAsync(ct);

    public Task<Sticker?> FindSticker(int id, CancellationToken ct) =>
        Stickers.SingleOrDefaultAsync(sticker => sticker.Id == id, ct);

    public async Task AddSticker(Sticker sticker, CancellationToken ct) => await db.Stickers.AddAsync(sticker, ct);

    public async Task DeleteTextSticker(int id, CancellationToken ct) =>
        await DesktopStickers.Where(sticker => sticker.Id == id && sticker.Text != null).ExecuteDeleteAsync(ct);

    public Task<bool> IsTaskPinned(int taskId, CancellationToken ct) =>
        DesktopStickers.AnyAsync(sticker => sticker.TaskId == taskId, ct);

    public async Task<Sticker?> PinTask(int taskId, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO Stickers (DesktopId, TaskId)
            SELECT {Desktop.DefaultId}, Id FROM ProjectTasks WHERE Id = {taskId}
            ON CONFLICT (DesktopId, TaskId) DO NOTHING;
            """, ct);
        return await Stickers.AsNoTracking().SingleOrDefaultAsync(sticker => sticker.TaskId == taskId, ct);
    }

    public async Task UnpinTask(int taskId, CancellationToken ct) =>
        await DesktopStickers.Where(sticker => sticker.TaskId == taskId).ExecuteDeleteAsync(ct);

    public Task<bool> IsNotePinned(int noteId, CancellationToken ct) =>
        DesktopStickers.AnyAsync(sticker => sticker.NoteId == noteId, ct);

    public async Task<Sticker?> PinNote(int noteId, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO Stickers (DesktopId, NoteId)
            SELECT {Desktop.DefaultId}, Id FROM Note WHERE Id = {noteId}
            ON CONFLICT (DesktopId, NoteId) DO NOTHING;
            """, ct);
        return await Stickers.AsNoTracking().SingleOrDefaultAsync(sticker => sticker.NoteId == noteId, ct);
    }

    public async Task UnpinNote(int noteId, CancellationToken ct) =>
        await DesktopStickers.Where(sticker => sticker.NoteId == noteId).ExecuteDeleteAsync(ct);

    public Task SaveChanges(CancellationToken ct) => db.SaveChangesAsync(ct);
}
