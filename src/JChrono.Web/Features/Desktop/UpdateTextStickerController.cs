using JChrono.Application;
using JChrono.Application.Features.Desktop;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Desktop;

[ApiController]
[Route("api/desktop/stickers")]
public sealed class UpdateTextStickerController(UpdateTextSticker handler) : ControllerBase
{
    [HttpPut("{id:int}/text")]
    public Task<StickerDto> Update(int id, SaveTextStickerRequest request, CancellationToken ct) =>
        handler.Execute(id, request, ct);
}
