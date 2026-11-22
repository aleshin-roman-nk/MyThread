using JChrono.Domain;

namespace JChrono.Application.Features.Desktop;

public interface ICreateTextStickerStore
{
    Task AddSticker(Sticker sticker, CancellationToken ct);
    Task SaveChanges(CancellationToken ct);
}

public sealed class CreateTextSticker(ICreateTextStickerStore store)
{
    public async Task<StickerDto> Execute(SaveTextStickerRequest request, CancellationToken ct)
    {
        var sticker = new Sticker(request.Text);
        await store.AddSticker(sticker, ct);
        await store.SaveChanges(ct);
        return StickerResults.ToDto(sticker);
    }
}
