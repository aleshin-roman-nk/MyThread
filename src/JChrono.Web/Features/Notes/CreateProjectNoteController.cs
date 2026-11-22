using JChrono.Application;
using JChrono.Application.Features.Notes;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Notes;

[ApiController]
[Route("api/projects/{id:int}/notes")]
public sealed class CreateProjectNoteController(CreateProjectNote handler) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ProjectNoteDto>> Create(int id, SaveProjectNoteRequest request, CancellationToken ct)
    {
        var note = await handler.Execute(id, request, ct);
        return Created($"/api/projects/{id}/notes", note);
    }
}
