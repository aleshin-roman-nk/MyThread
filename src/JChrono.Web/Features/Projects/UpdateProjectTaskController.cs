using JChrono.Application;
using JChrono.Application.Features.Projects;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Projects;

[ApiController]
[Route("api/projects/{projectId:int}/tasks")]
public sealed class UpdateProjectTaskController(UpdateProjectTask handler) : ControllerBase
{
    [HttpPut("{id:int}")]
    public Task<ProjectTaskDto> Update(int projectId, int id, UpdateProjectTaskRequest request, CancellationToken ct) =>
        handler.Execute(projectId, id, request, ct);
}
