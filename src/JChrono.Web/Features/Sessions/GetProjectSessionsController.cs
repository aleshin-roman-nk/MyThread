using JChrono.Application;
using JChrono.Application.Features.Sessions;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Sessions;

[ApiController]
public sealed class GetProjectSessionsController(GetProjectSessions handler) : ControllerBase
{
    [HttpGet("/api/projects/{projectId:int}/sessions")]
    public Task<IReadOnlyList<SessionDto>> Get(int projectId, CancellationToken ct) =>
        handler.Execute(projectId, ct);
}
