using JChrono.Application;
using JChrono.Application.Features.Projects;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Projects;

[ApiController]
[Route("api/projects")]
public sealed class SetProjectStatusController(SetProjectStatus handler) : ControllerBase
{
    [HttpPut("{id:int}/status")]
    public Task<ProjectDto> SetStatus(int id, SetProjectStatusRequest request, CancellationToken ct) =>
        handler.Execute(id, request, ct);
}
