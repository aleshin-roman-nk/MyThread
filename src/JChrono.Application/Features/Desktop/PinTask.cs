using JChrono.Domain;

namespace JChrono.Application.Features.Desktop;

public interface IPinTaskStore
{
    Task<Sticker?> PinTask(int taskId, CancellationToken ct);
}

public sealed class PinTask(IPinTaskStore store)
{
    public async Task<StickerDto> Execute(int taskId, CancellationToken ct)
    {
        var sticker = await store.PinTask(taskId, ct) ?? throw new NotFoundException("Задача не найдена.");
        return StickerResults.ToDto(sticker);
    }
}
