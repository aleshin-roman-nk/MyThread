using JChrono.Application;
using JChrono.Application.Features.Sessions;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Sessions;

[ApiController]
[Route("api/sessions/current")]
public sealed class GetCurrentSessionController(GetCurrentSession handler) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SessionDto>> Get(CancellationToken ct) =>
        await handler.Execute(ct) is { } session ? Ok(session) : NoContent();
}
