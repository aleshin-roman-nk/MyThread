using JChrono.Domain;

namespace JChrono.Application.Features.Projects;

public interface IProjectTasksQuery
{
    Task<Project?> FindProjectWithTasks(int projectId, ProjectTaskStatus? status, CancellationToken ct);
}

public sealed class GetProjectTasks(IProjectTasksQuery query)
{
    public async Task<IReadOnlyList<ProjectTaskDto>> Execute(int projectId, ProjectTaskStatus? status, CancellationToken ct)
    {
        if (status.HasValue && !Enum.IsDefined(status.Value))
            throw new ArgumentException("Неизвестный статус задачи.");

        var project = await query.FindProjectWithTasks(projectId, status, ct)
            ?? throw new NotFoundException("Проект не найден.");
        return ProjectResults.ToTasks(project.Tasks);
    }
}
