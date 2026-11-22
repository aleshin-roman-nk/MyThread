using JChrono.Application;
using JChrono.Application.Features.Sessions;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Sessions;

[ApiController]
[Route("api/sessions")]
public sealed class UpdateSessionController(UpdateSession handler) : ControllerBase
{
    [HttpPut("{id:int}")]
    public Task<SessionDto> Update(int id, UpdateSessionRequest request, CancellationToken ct) =>
        handler.Execute(id, request, ct);
}
