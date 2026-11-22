using JChrono.Application;
using JChrono.Application.Features.Notes;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Notes;

[ApiController]
[Route("api/notes")]
public sealed class CreateNoteController(CreateNote handler) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<NoteDto>> Create(SaveNoteRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await handler.Execute(request, ct));
}
