using JChrono.Domain;

namespace JChrono.Application.Features.Notes;

public interface IDeleteNoteStore
{
    Task<Note?> FindSubjectNote(int id, CancellationToken ct);
    void RemoveNote(Note note);
    Task SaveChanges(CancellationToken ct);
}

public sealed class DeleteNote(IDeleteNoteStore store)
{
    public async Task Execute(int id, CancellationToken ct)
    {
        var note = await store.FindSubjectNote(id, ct) ?? throw new NotFoundException("Заметка не найдена.");
        store.RemoveNote(note);
        await store.SaveChanges(ct);
    }
}
