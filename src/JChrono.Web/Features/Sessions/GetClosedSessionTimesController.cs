using JChrono.Application;
using JChrono.Application.Features.Sessions;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Sessions;

[ApiController]
[Route("api/sessions/closed-times")]
public sealed class GetClosedSessionTimesController(GetClosedSessionTimes handler) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<ClosedSessionTimeDto>> Get(DateTimeOffset? startTime, DateTimeOffset? endTime, CancellationToken ct) =>
        handler.Execute(startTime, endTime, ct);
}
