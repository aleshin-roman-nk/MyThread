using JChrono.Domain;

namespace JChrono.Application.Features.Desktop;

public interface IMoveStickerStore
{
    Task<Sticker?> FindSticker(int id, CancellationToken ct);
    Task SaveChanges(CancellationToken ct);
}

public sealed class MoveSticker(IMoveStickerStore store)
{
    public async Task<StickerDto> Execute(int id, MoveStickerRequest request, CancellationToken ct)
    {
        var sticker = await store.FindSticker(id, ct) ?? throw new NotFoundException("Стикер не найден.");
        sticker.Move(request.X, request.Y);
        await store.SaveChanges(ct);
        return StickerResults.ToDto(sticker);
    }
}
