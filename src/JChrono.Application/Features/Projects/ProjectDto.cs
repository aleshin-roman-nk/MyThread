using JChrono.Domain;

namespace JChrono.Application;

public sealed record ProjectDto(int Id, int SubjectId, string Name, string Code, string FullCode,
    IReadOnlyList<ProjectTaskDto> Tasks, ProjectStatus Status, string Description, DateTimeOffset? CreatedAt = null);
