using JChrono.Domain;

namespace JChrono.Application.Features.Projects;

public interface IUpdateProjectStore
{
    Task<Project?> FindProject(int id, CancellationToken ct);
    Task SaveChanges(CancellationToken ct);
}

public sealed class UpdateProject(IUpdateProjectStore store)
{
    public async Task<ProjectDto> Execute(int id, UpdateProjectRequest request, CancellationToken ct)
    {
        var project = await store.FindProject(id, ct) ?? throw new NotFoundException("Проект не найден.");
        project.Update(request.Name, request.Status, request.Description);
        await store.SaveChanges(ct);
        return ProjectResults.ToDto(project);
    }
}
