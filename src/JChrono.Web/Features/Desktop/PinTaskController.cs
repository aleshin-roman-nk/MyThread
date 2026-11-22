using JChrono.Application;
using JChrono.Application.Features.Desktop;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Desktop;

[ApiController]
[Route("api/desktop/stickers/tasks/{taskId:int}")]
public sealed class PinTaskController(PinTask handler) : ControllerBase
{
    [HttpPut]
    public Task<StickerDto> Pin(int taskId, CancellationToken ct) => handler.Execute(taskId, ct);
}
