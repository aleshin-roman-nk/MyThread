using JChrono.Application;
using JChrono.Application.Features.Projects;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Projects;

[ApiController]
[Route("api/projects")]
public sealed class DeleteProjectController(DeleteProject handler) : ControllerBase
{
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, [FromBody] DeleteProjectRequest request, CancellationToken ct)
    {
        await handler.Execute(id, request, ct);
        return NoContent();
    }
}
