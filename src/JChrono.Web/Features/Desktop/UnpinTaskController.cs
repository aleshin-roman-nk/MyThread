using JChrono.Application.Features.Desktop;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Desktop;

[ApiController]
[Route("api/desktop/stickers/tasks/{taskId:int}")]
public sealed class UnpinTaskController(UnpinTask handler) : ControllerBase
{
    [HttpDelete]
    public async Task<IActionResult> Unpin(int taskId, CancellationToken ct)
    {
        await handler.Execute(taskId, ct);
        return NoContent();
    }
}
