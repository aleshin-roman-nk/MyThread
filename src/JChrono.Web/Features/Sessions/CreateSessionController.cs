using JChrono.Application;
using JChrono.Application.Features.Sessions;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Sessions;

[ApiController]
[Route("api/sessions")]
public sealed class CreateSessionController(CreateSession handler) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<SessionDto>> Create(CreateSessionRequest request, CancellationToken ct)
    {
        var session = await handler.Execute(request, ct);
        return CreatedAtRoute(GetSessionController.RouteName, new { id = session.Id }, session);
    }
}
