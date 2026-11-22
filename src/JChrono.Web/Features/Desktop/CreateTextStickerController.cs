using JChrono.Application;
using JChrono.Application.Features.Desktop;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Desktop;

[ApiController]
[Route("api/desktop/stickers")]
public sealed class CreateTextStickerController(CreateTextSticker handler) : ControllerBase
{
    [HttpPost("text")]
    public Task<StickerDto> Create(SaveTextStickerRequest request, CancellationToken ct) =>
        handler.Execute(request, ct);
}
