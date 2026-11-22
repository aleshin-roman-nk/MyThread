using JChrono.Application;
using JChrono.Application.Features.Projects;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Projects;

[ApiController]
[Route("api/projects")]
public sealed class UpdateProjectController(UpdateProject handler) : ControllerBase
{
    [HttpPut("{id:int}")]
    public Task<ProjectDto> Update(int id, UpdateProjectRequest request, CancellationToken ct) =>
        handler.Execute(id, request, ct);
}
