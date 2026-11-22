using JChrono.Application.Features.Desktop;
using JChrono.Domain;
using Microsoft.EntityFrameworkCore;

namespace JChrono.Infrastructure.Features.Desktop;

public sealed class StickersQuery(NotesDbContext db) : IStickersQuery
{
    public async Task<IReadOnlyList<Sticker>> List(CancellationToken ct) =>
        await db.DefaultDesktopStickers().WithStickerContent().AsNoTracking()
            .OrderBy(sticker => sticker.Id).ToListAsync(ct);
}
