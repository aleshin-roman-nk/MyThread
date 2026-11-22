using JChrono.Domain;

namespace JChrono.Application.Features.Projects;

internal static class ProjectResults
{
    public static ProjectDto ToDto(Project project) => new(project.Id, project.SubjectId, project.Name,
        project.Code, project.FullCode, ToTasks(project.Tasks),
        project.Status, project.Description, project.CreatedAt);

    public static IReadOnlyList<ProjectTaskDto> ToTasks(IEnumerable<ProjectTask> tasks) =>
        tasks.OrderBy(t => t.Name).ThenBy(t => t.Id).Select(ToDto).ToList();

    public static ProjectTaskDto ToDto(ProjectTask task) =>
        new(task.Id, task.ProjectId, task.Name, task.Code, task.FullCode, task.Body, task.Status, task.CreatedAt);
}
