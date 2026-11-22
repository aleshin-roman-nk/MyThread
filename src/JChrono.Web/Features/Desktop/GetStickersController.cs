using JChrono.Application;
using JChrono.Application.Features.Desktop;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Desktop;

[ApiController]
[Route("api/desktop/stickers")]
public sealed class GetStickersController(GetStickers handler) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<StickerDto>> Get(CancellationToken ct) => handler.Execute(ct);
}
