using JChrono.Application;
using JChrono.Application.Features.Notes;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Notes;

[ApiController]
[Route("api/subjects/{id:int}/notes")]
public sealed class GetSubjectNotesController(GetSubjectNotes handler) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<NoteDto>> Get(int id, CancellationToken ct) => handler.Execute(id, ct);
}
