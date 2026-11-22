using JChrono.Application;
using JChrono.Application.Features.Sessions;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Sessions;

[ApiController]
[Route("api/sessions")]
public sealed class GetDaySessionsController(GetDaySessions handler) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<DaySessionDto>> Get(DateTimeOffset? startTime, DateTimeOffset? endTime, CancellationToken ct) =>
        handler.Execute(startTime, endTime, ct);
}
