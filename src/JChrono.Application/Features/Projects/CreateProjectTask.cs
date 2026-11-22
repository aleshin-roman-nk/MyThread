using JChrono.Domain;

namespace JChrono.Application.Features.Projects;

public interface ICreateProjectTaskStore
{
    Task<ProjectTask> CreateProjectTask(int projectId, string name, string body, CancellationToken ct);
}

public sealed class CreateProjectTask(ICreateProjectTaskStore store)
{
    public async Task<ProjectTaskDto> Execute(int projectId, CreateProjectTaskRequest request, CancellationToken ct)
    {
        var task = await store.CreateProjectTask(projectId, request.Name, request.Body, ct);
        return ProjectResults.ToDto(task);
    }
}
