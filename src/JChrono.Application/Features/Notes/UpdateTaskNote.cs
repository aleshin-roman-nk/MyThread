using JChrono.Domain;

namespace JChrono.Application.Features.Notes;

public interface IUpdateTaskNoteStore
{
    Task<Note?> FindTaskNote(int projectId, int taskId, int id, CancellationToken ct);
    Task SaveChanges(CancellationToken ct);
}

public sealed class UpdateTaskNote(IUpdateTaskNoteStore store)
{
    public async Task<TaskNoteDto> Execute(int projectId, int taskId, int id, SaveTaskNoteRequest request, CancellationToken ct)
    {
        var note = await store.FindTaskNote(projectId, taskId, id, ct)
            ?? throw new NotFoundException("Заметка задачи не найдена.");
        note.Update(request.Date, request.Time ?? note.Time, request.Text, request.Rating ?? note.Rating);
        await store.SaveChanges(ct);
        return TaskNoteDto.FromNote(note, taskId);
    }
}
