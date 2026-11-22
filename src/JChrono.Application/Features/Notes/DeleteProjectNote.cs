using JChrono.Domain;

namespace JChrono.Application.Features.Notes;

public interface IDeleteProjectNoteStore
{
    Task<Note?> FindProjectNote(int projectId, int id, CancellationToken ct);
    void RemoveNote(Note note);
    Task SaveChanges(CancellationToken ct);
}

public sealed class DeleteProjectNote(IDeleteProjectNoteStore store)
{
    public async Task Execute(int projectId, int id, CancellationToken ct)
    {
        var note = await store.FindProjectNote(projectId, id, ct)
            ?? throw new NotFoundException("Заметка проекта не найдена.");
        store.RemoveNote(note);
        await store.SaveChanges(ct);
    }
}
