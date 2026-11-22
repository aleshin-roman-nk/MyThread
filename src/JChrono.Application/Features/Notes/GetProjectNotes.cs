namespace JChrono.Application.Features.Notes;

public interface IProjectNotesQuery
{
    Task<bool> ProjectExists(int projectId, CancellationToken ct);
    Task<IReadOnlyList<ProjectNoteDto>> ListByProject(int projectId, CancellationToken ct);
}

public sealed class GetProjectNotes(IProjectNotesQuery query)
{
    public async Task<IReadOnlyList<ProjectNoteDto>> Execute(int projectId, CancellationToken ct)
    {
        if (!await query.ProjectExists(projectId, ct))
            throw new NotFoundException("Проект не найден.");

        return await query.ListByProject(projectId, ct);
    }
}
