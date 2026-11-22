using JChrono.Application;
using JChrono.Application.Features.Sessions;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Sessions;

[ApiController]
[Route("api/sessions/useful-work")]
public sealed class GetUsefulWorkController(GetUsefulWork handler) : ControllerBase
{
    [HttpGet]
    public Task<UsefulWorkDto> Get(DateTimeOffset? startTime, DateTimeOffset? endTime, CancellationToken ct) =>
        handler.Execute(startTime, endTime, ct);
}
