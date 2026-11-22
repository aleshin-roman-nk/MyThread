using JChrono.Domain;

namespace JChrono.Application.Features.Notes;

public interface ICreateProjectNoteStore
{
    Task<Project?> FindProject(int id, CancellationToken ct);
    Task AddNote(Note note, CancellationToken ct);
    Task SaveChanges(CancellationToken ct);
}

public sealed class CreateProjectNote(ICreateProjectNoteStore store)
{
    public async Task<ProjectNoteDto> Execute(int projectId, SaveProjectNoteRequest request, CancellationToken ct)
    {
        if (await store.FindProject(projectId, ct) is null)
            throw new NotFoundException("Проект не найден.");

        var note = Note.ForProject(projectId, request.Date, request.Time ?? TimeOnly.MinValue,
            request.Text, request.Rating ?? Note.DefaultRating);
        await store.AddNote(note, ct);
        await store.SaveChanges(ct);
        return ProjectNoteDto.FromNote(note, projectId);
    }
}
