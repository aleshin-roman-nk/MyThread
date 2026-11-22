using JChrono.Domain;

namespace JChrono.Application.Features.Desktop;

public interface IStickersQuery
{
    Task<IReadOnlyList<Sticker>> List(CancellationToken ct);
}

public sealed class GetStickers(IStickersQuery query)
{
    public async Task<IReadOnlyList<StickerDto>> Execute(CancellationToken ct) =>
        (await query.List(ct)).Select(StickerResults.ToDto).ToList();
}
