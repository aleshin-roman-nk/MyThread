using JChrono.Application.Features.Desktop;
using JChrono.Domain;
using JChrono.Infrastructure.Features.Desktop;
using Microsoft.EntityFrameworkCore;

namespace JChrono.Infrastructure;

public sealed class DesktopRepository(NotesDbContext db)
    : ICreateTextStickerStore, IUpdateTextStickerStore, IDeleteTextStickerStore, IMoveStickerStore
{
    private IQueryable<Sticker> DesktopStickers => db.DefaultDesktopStickers();

    private IQueryable<Sticker> Stickers => DesktopStickers.WithStickerContent();

    public Task<Sticker?> FindSticker(int id, CancellationToken ct) =>
        Stickers.SingleOrDefaultAsync(sticker => sticker.Id == id, ct);

    public async Task AddSticker(Sticker sticker, CancellationToken ct) => await db.Stickers.AddAsync(sticker, ct);

    public async Task DeleteTextSticker(int id, CancellationToken ct) =>
        await DesktopStickers.Where(sticker => sticker.Id == id && sticker.Text != null).ExecuteDeleteAsync(ct);

    public Task SaveChanges(CancellationToken ct) => db.SaveChangesAsync(ct);
}
