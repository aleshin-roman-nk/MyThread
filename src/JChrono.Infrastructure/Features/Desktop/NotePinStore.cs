using JChrono.Application.Features.Desktop;
using JChrono.Domain;
using Microsoft.EntityFrameworkCore;

namespace JChrono.Infrastructure.Features.Desktop;

public sealed class NotePinStore(NotesDbContext db) : INotePinStatusQuery, IPinNoteStore, IUnpinNoteStore
{
    public Task<bool> IsNotePinned(int noteId, CancellationToken ct) =>
        db.DefaultDesktopStickers().AnyAsync(sticker => sticker.NoteId == noteId, ct);

    public async Task<Sticker?> PinNote(int noteId, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO Stickers (DesktopId, NoteId)
            SELECT {JChrono.Domain.Desktop.DefaultId}, Id FROM Note WHERE Id = {noteId}
            ON CONFLICT (DesktopId, NoteId) DO NOTHING;
            """, ct);
        return await db.DefaultDesktopStickers().WithStickerContent().AsNoTracking()
            .SingleOrDefaultAsync(sticker => sticker.NoteId == noteId, ct);
    }

    public async Task UnpinNote(int noteId, CancellationToken ct) =>
        await db.DefaultDesktopStickers().Where(sticker => sticker.NoteId == noteId).ExecuteDeleteAsync(ct);
}
