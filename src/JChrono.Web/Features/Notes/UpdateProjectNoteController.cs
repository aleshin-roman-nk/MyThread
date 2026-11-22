using JChrono.Application;
using JChrono.Application.Features.Notes;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Notes;

[ApiController]
[Route("api/projects/{id:int}/notes")]
public sealed class UpdateProjectNoteController(UpdateProjectNote handler) : ControllerBase
{
    [HttpPut("{noteId:int}")]
    public Task<ProjectNoteDto> Update(int id, int noteId, SaveProjectNoteRequest request, CancellationToken ct) =>
        handler.Execute(id, noteId, request, ct);
}
