using JChrono.Domain;

namespace JChrono.Application.Features.Projects;

public interface IUpdateProjectTaskStore
{
    Task<ProjectTask?> FindProjectTask(int projectId, int id, CancellationToken ct);
    Task SaveChanges(CancellationToken ct);
}

public sealed class UpdateProjectTask(IUpdateProjectTaskStore store)
{
    public async Task<ProjectTaskDto> Execute(int projectId, int id, UpdateProjectTaskRequest request, CancellationToken ct)
    {
        var task = await store.FindProjectTask(projectId, id, ct) ?? throw new NotFoundException("Задача не найдена.");
        task.Update(request.Name, request.Body, request.Status);
        await store.SaveChanges(ct);
        return ProjectResults.ToDto(task);
    }
}
