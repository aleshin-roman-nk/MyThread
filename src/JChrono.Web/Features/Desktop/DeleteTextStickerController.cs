using JChrono.Application.Features.Desktop;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Desktop;

[ApiController]
[Route("api/desktop/stickers")]
public sealed class DeleteTextStickerController(DeleteTextSticker handler) : ControllerBase
{
    [HttpDelete("{id:int}/text")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await handler.Execute(id, ct);
        return NoContent();
    }
}
