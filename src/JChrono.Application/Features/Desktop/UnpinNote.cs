namespace JChrono.Application.Features.Desktop;

public interface IUnpinNoteStore
{
    Task UnpinNote(int noteId, CancellationToken ct);
}

public sealed class UnpinNote(IUnpinNoteStore store)
{
    public Task Execute(int noteId, CancellationToken ct) => store.UnpinNote(noteId, ct);
}
