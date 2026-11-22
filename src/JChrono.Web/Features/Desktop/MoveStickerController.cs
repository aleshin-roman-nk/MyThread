using JChrono.Application;
using JChrono.Application.Features.Desktop;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Desktop;

[ApiController]
[Route("api/desktop/stickers")]
public sealed class MoveStickerController(MoveSticker handler) : ControllerBase
{
    [HttpPut("{id:int}/position")]
    public Task<StickerDto> Move(int id, MoveStickerRequest request, CancellationToken ct) =>
        handler.Execute(id, request, ct);
}
