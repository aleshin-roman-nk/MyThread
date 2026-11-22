using JChrono.Application;
using JChrono.Application.Features.Projects;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Projects;

[ApiController]
[Route("api/projects/{projectId:int}/tasks")]
public sealed class DeleteProjectTaskController(DeleteProjectTask handler) : ControllerBase
{
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int projectId, int id, [FromBody] DeleteTaskRequest request, CancellationToken ct)
    {
        await handler.Execute(projectId, id, request, ct);
        return NoContent();
    }
}
