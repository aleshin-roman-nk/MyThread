using JChrono.Application;
using JChrono.Application.Features.Sessions;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Sessions;

[ApiController]
public sealed class GetTaskSessionsController(GetTaskSessions handler) : ControllerBase
{
    [HttpGet("/api/projects/{projectId:int}/tasks/{taskId:int}/sessions")]
    public Task<IReadOnlyList<SessionDto>> Get(int projectId, int taskId, CancellationToken ct) =>
        handler.Execute(projectId, taskId, ct);
}
