using JChrono.Domain;

namespace JChrono.Application.Features.Notes;

public interface IUpdateProjectNoteStore
{
    Task<Note?> FindProjectNote(int projectId, int id, CancellationToken ct);
    Task SaveChanges(CancellationToken ct);
}

public sealed class UpdateProjectNote(IUpdateProjectNoteStore store)
{
    public async Task<ProjectNoteDto> Execute(int projectId, int id, SaveProjectNoteRequest request, CancellationToken ct)
    {
        var note = await store.FindProjectNote(projectId, id, ct)
            ?? throw new NotFoundException("Заметка проекта не найдена.");
        note.Update(request.Date, request.Time ?? note.Time, request.Text, request.Rating ?? note.Rating);
        await store.SaveChanges(ct);
        return ProjectNoteDto.FromNote(note, projectId);
    }
}
