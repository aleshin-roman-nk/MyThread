using JChrono.Application;
using JChrono.Application.Features.Projects;
using JChrono.Domain;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Projects;

[ApiController]
[Route("api/projects/{projectId:int}/tasks")]
public sealed class GetProjectTasksController(GetProjectTasks handler) : ControllerBase
{
    internal const string RouteName = "GetProjectTasks";

    [HttpGet(Name = RouteName)]
    public Task<IReadOnlyList<ProjectTaskDto>> Get(int projectId, ProjectTaskStatus? status, CancellationToken ct) =>
        handler.Execute(projectId, status, ct);
}
