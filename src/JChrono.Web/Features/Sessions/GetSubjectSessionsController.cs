using JChrono.Application;
using JChrono.Application.Features.Sessions;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Sessions;

[ApiController]
public sealed class GetSubjectSessionsController(GetSubjectSessions handler) : ControllerBase
{
    [HttpGet("/api/subjects/{subjectId:int}/sessions")]
    public Task<IReadOnlyList<SessionDto>> Get(int subjectId, CancellationToken ct) =>
        handler.Execute(subjectId, ct);
}
