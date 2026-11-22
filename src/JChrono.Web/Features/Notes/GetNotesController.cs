using JChrono.Application;
using JChrono.Application.Features.Notes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace JChrono.Web.Features.Notes;

[ApiController]
[Route("api/notes")]
public sealed class GetNotesController(GetNotes handler) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<NoteDto>> Get([FromQuery, BindRequired] DateOnly date, CancellationToken ct) =>
        handler.Execute(date, ct);
}
