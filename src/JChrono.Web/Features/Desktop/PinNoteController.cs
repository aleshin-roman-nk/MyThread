using JChrono.Application;
using JChrono.Application.Features.Desktop;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Desktop;

[ApiController]
[Route("api/desktop/stickers/notes/{noteId:int}")]
public sealed class PinNoteController(PinNote handler) : ControllerBase
{
    [HttpPut]
    public Task<StickerDto> Pin(int noteId, CancellationToken ct) => handler.Execute(noteId, ct);
}
