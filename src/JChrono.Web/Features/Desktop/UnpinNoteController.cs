using JChrono.Application.Features.Desktop;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Desktop;

[ApiController]
[Route("api/desktop/stickers/notes/{noteId:int}")]
public sealed class UnpinNoteController(UnpinNote handler) : ControllerBase
{
    [HttpDelete]
    public async Task<IActionResult> Unpin(int noteId, CancellationToken ct)
    {
        await handler.Execute(noteId, ct);
        return NoContent();
    }
}
