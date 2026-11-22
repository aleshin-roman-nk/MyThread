using JChrono.Application;
using JChrono.Application.Features.Projects;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Projects;

[ApiController]
[Route("api/projects")]
public sealed class GetProjectController(GetProject handler) : ControllerBase
{
    [HttpGet("{id:int}")]
    public Task<ProjectDto> Get(int id, CancellationToken ct) => handler.Execute(id, ct);
}
