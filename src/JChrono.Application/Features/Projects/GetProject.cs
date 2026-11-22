using JChrono.Domain;

namespace JChrono.Application.Features.Projects;

public interface IProjectQuery
{
    Task<Project?> Find(int id, CancellationToken ct);
}

public sealed class GetProject(IProjectQuery query)
{
    public async Task<ProjectDto> Execute(int id, CancellationToken ct)
    {
        var project = await query.Find(id, ct) ?? throw new NotFoundException("Проект не найден.");
        return ProjectResults.ToDto(project);
    }
}
