using JChrono.Application;
using JChrono.Application.Features.Notes;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Notes;

[ApiController]
[Route("api/projects/{id:int}/notes")]
public sealed class GetProjectNotesController(GetProjectNotes handler) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<ProjectNoteDto>> Get(int id, CancellationToken ct) => handler.Execute(id, ct);
}
