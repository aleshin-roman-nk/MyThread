using JChrono.Domain;

namespace JChrono.Application.Features.Projects;

public interface ICreateProjectStore
{
    Task<Project> CreateProject(int subjectId, string name, string description, CancellationToken ct);
}

public sealed class CreateProject(ICreateProjectStore store)
{
    public async Task<ProjectDto> Execute(int subjectId, CreateProjectRequest request, CancellationToken ct)
    {
        var project = await store.CreateProject(subjectId, request.Name, request.Description, ct);
        return ProjectResults.ToDto(project);
    }
}
