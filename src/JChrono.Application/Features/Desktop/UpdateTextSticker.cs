using JChrono.Domain;

namespace JChrono.Application.Features.Desktop;

public interface IUpdateTextStickerStore
{
    Task<Sticker?> FindSticker(int id, CancellationToken ct);
    Task SaveChanges(CancellationToken ct);
}

public sealed class UpdateTextSticker(IUpdateTextStickerStore store)
{
    public async Task<StickerDto> Execute(int id, SaveTextStickerRequest request, CancellationToken ct)
    {
        var sticker = await store.FindSticker(id, ct) ?? throw new NotFoundException("Стикер не найден.");
        sticker.UpdateText(request.Text);
        await store.SaveChanges(ct);
        return StickerResults.ToDto(sticker);
    }
}
