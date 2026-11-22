using JChrono.Domain;

namespace JChrono.Application.Features.Projects;

public interface IProjectsQuery
{
    Task<bool> SubjectExists(int subjectId, CancellationToken ct);
    Task<IReadOnlyList<Project>> List(int subjectId, ProjectStatus? status, CancellationToken ct);
}

public sealed class GetProjects(IProjectsQuery query)
{
    public async Task<IReadOnlyList<ProjectDto>> Execute(int subjectId, ProjectStatus? status, CancellationToken ct)
    {
        if (status.HasValue && !Enum.IsDefined(status.Value))
            throw new ArgumentException("Неизвестное состояние проекта.");
        if (!await query.SubjectExists(subjectId, ct))
            throw new NotFoundException("Предмет не найден.");

        return (await query.List(subjectId, status, ct)).Select(ProjectResults.ToDto).ToList();
    }
}
