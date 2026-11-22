using JChrono.Application;
using JChrono.Application.Features.Projects;
using JChrono.Domain;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Projects;

[ApiController]
[Route("api/subjects/{id:int}/projects")]
public sealed class GetProjectsController(GetProjects handler) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<ProjectDto>> Get(int id, ProjectStatus? status, CancellationToken ct) =>
        handler.Execute(id, status, ct);
}
