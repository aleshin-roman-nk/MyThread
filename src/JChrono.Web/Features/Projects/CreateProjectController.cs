using JChrono.Application;
using JChrono.Application.Features.Projects;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Projects;

[ApiController]
[Route("api/subjects/{id:int}/projects")]
public sealed class CreateProjectController(CreateProject handler) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ProjectDto>> Create(int id, CreateProjectRequest request, CancellationToken ct)
    {
        var project = await handler.Execute(id, request, ct);
        return Created($"/api/subjects/{id}/projects", project);
    }
}
