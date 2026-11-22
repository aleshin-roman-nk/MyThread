using JChrono.Application;
using JChrono.Application.Features.Notes;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Notes;

[ApiController]
[Route("api/notes")]
public sealed class UpdateNoteController(UpdateNote handler) : ControllerBase
{
    [HttpPut("{id:int}")]
    public Task<NoteDto> Update(int id, SaveNoteRequest request, CancellationToken ct) =>
        handler.Execute(id, request, ct);
}
