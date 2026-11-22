using JChrono.Application;
using JChrono.Application.Features.Sessions;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Sessions;

[ApiController]
[Route("api/sessions")]
public sealed class CloseSessionController(CloseSession handler) : ControllerBase
{
    [HttpPost("{id:int}/close")]
    public Task<SessionDto> Close(int id, CancellationToken ct) => handler.Execute(id, ct);
}
