using JChrono.Application;
using JChrono.Application.Features.Sessions;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Sessions;

[ApiController]
[Route("api/sessions")]
public sealed class UpdateSessionTextController(UpdateSessionText handler) : ControllerBase
{
    [HttpPut("{id:int}/text")]
    public Task<SessionDto> UpdateText(int id, UpdateSessionTextRequest request, CancellationToken ct) =>
        handler.Execute(id, request, ct);
}
