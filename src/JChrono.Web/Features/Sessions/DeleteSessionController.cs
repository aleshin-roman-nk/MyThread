using JChrono.Application.Features.Sessions;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Sessions;

[ApiController]
[Route("api/sessions")]
public sealed class DeleteSessionController(DeleteSession handler) : ControllerBase
{
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await handler.Execute(id, ct);
        return NoContent();
    }
}
