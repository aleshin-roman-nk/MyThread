using JChrono.Domain;
using Microsoft.EntityFrameworkCore;

namespace JChrono.Infrastructure.Features.Desktop;

internal static class StickerQueries
{
    public static IQueryable<Sticker> DefaultDesktopStickers(this NotesDbContext db) =>
        db.Stickers.Where(sticker => sticker.DesktopId == JChrono.Domain.Desktop.DefaultId);

    public static IQueryable<Sticker> WithStickerContent(this IQueryable<Sticker> stickers) =>
        stickers.Include(sticker => sticker.Task).ThenInclude(task => task!.Project).ThenInclude(project => project.Subject)
            .Include(sticker => sticker.Note).ThenInclude(note => note!.Subject)
            .Include(sticker => sticker.Note).ThenInclude(note => note!.Project)
            .Include(sticker => sticker.Note).ThenInclude(note => note!.Task);
}
