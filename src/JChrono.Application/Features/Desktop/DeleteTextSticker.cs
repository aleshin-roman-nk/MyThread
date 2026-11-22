namespace JChrono.Application.Features.Desktop;

public interface IDeleteTextStickerStore
{
    Task DeleteTextSticker(int id, CancellationToken ct);
}

public sealed class DeleteTextSticker(IDeleteTextStickerStore store)
{
    public Task Execute(int id, CancellationToken ct) => store.DeleteTextSticker(id, ct);
}
