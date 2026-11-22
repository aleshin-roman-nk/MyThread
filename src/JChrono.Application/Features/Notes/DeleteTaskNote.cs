using JChrono.Domain;

namespace JChrono.Application.Features.Notes;

public interface IDeleteTaskNoteStore
{
    Task<Note?> FindTaskNote(int projectId, int taskId, int id, CancellationToken ct);
    void RemoveNote(Note note);
    Task SaveChanges(CancellationToken ct);
}

public sealed class DeleteTaskNote(IDeleteTaskNoteStore store)
{
    public async Task Execute(int projectId, int taskId, int id, CancellationToken ct)
    {
        var note = await store.FindTaskNote(projectId, taskId, id, ct)
            ?? throw new NotFoundException("Заметка задачи не найдена.");
        store.RemoveNote(note);
        await store.SaveChanges(ct);
    }
}
