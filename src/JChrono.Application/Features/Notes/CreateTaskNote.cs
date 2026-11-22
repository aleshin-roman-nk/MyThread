using JChrono.Domain;

namespace JChrono.Application.Features.Notes;

public interface ICreateTaskNoteStore
{
    Task<ProjectTask?> FindProjectTask(int projectId, int id, CancellationToken ct);
    Task AddNote(Note note, CancellationToken ct);
    Task SaveChanges(CancellationToken ct);
}

public sealed class CreateTaskNote(ICreateTaskNoteStore store)
{
    public async Task<TaskNoteDto> Execute(int projectId, int taskId, SaveTaskNoteRequest request, CancellationToken ct)
    {
        if (await store.FindProjectTask(projectId, taskId, ct) is null)
            throw new NotFoundException("Задача не найдена.");

        var note = Note.ForTask(taskId, request.Date, request.Time ?? TimeOnly.MinValue,
            request.Text, request.Rating ?? Note.DefaultRating);
        await store.AddNote(note, ct);
        await store.SaveChanges(ct);
        return TaskNoteDto.FromNote(note, taskId);
    }
}
