using JChrono.Domain;

namespace JChrono.Application.Features.Desktop;

public interface IPinNoteStore
{
    Task<Sticker?> PinNote(int noteId, CancellationToken ct);
}

public sealed class PinNote(IPinNoteStore store)
{
    public async Task<StickerDto> Execute(int noteId, CancellationToken ct)
    {
        var sticker = await store.PinNote(noteId, ct) ?? throw new NotFoundException("Заметка не найдена.");
        return StickerResults.ToDto(sticker);
    }
}
