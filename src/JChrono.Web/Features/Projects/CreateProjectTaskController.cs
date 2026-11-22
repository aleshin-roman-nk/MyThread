using JChrono.Application;
using JChrono.Application.Features.Projects;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Projects;

[ApiController]
[Route("api/projects/{projectId:int}/tasks")]
public sealed class CreateProjectTaskController(CreateProjectTask handler) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ProjectTaskDto>> Create(int projectId, CreateProjectTaskRequest request, CancellationToken ct)
    {
        var task = await handler.Execute(projectId, request, ct);
        return CreatedAtRoute(GetProjectTasksController.RouteName, new { projectId }, task);
    }
}
