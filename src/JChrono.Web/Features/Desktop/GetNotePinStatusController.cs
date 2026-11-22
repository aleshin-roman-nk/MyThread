using JChrono.Application;
using JChrono.Application.Features.Desktop;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Desktop;

[ApiController]
[Route("api/desktop/stickers/notes/{noteId:int}")]
public sealed class GetNotePinStatusController(GetNotePinStatus handler) : ControllerBase
{
    [HttpGet]
    public Task<TaskPinStatusDto> Get(int noteId, CancellationToken ct) => handler.Execute(noteId, ct);
}
