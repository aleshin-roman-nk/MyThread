using JChrono.Domain;

namespace JChrono.Application;

public sealed record UpdateProjectRequest(string Name, ProjectStatus Status, string Description = "");
