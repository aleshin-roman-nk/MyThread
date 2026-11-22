using JChrono.Domain;

namespace JChrono.Application;

public sealed record UpdateProjectTaskRequest(string Name, string Body, ProjectTaskStatus Status);
