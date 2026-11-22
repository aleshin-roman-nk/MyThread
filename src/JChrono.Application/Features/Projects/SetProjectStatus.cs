using JChrono.Domain;

namespace JChrono.Application.Features.Projects;

public interface ISetProjectStatusStore
{
    Task<Project?> FindProject(int id, CancellationToken ct);
    Task SaveChanges(CancellationToken ct);
}

public sealed class SetProjectStatus(ISetProjectStatusStore store)
{
    public async Task<ProjectDto> Execute(int id, SetProjectStatusRequest request, CancellationToken ct)
    {
        var project = await store.FindProject(id, ct) ?? throw new NotFoundException("Проект не найден.");
        project.SetStatus(request.Status);
        await store.SaveChanges(ct);
        return ProjectResults.ToDto(project);
    }
}
