namespace JChrono.Application.Features.Desktop;

public interface INotePinStatusQuery
{
    Task<bool> IsNotePinned(int noteId, CancellationToken ct);
}

public sealed class GetNotePinStatus(INotePinStatusQuery query)
{
    public async Task<TaskPinStatusDto> Execute(int noteId, CancellationToken ct) =>
        new(await query.IsNotePinned(noteId, ct));
}
