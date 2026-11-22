using JChrono.Application;
using JChrono.Application.Features.Notes;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Notes;

[ApiController]
[Route("api/projects/{projectId:int}/tasks/{id:int}/notes")]
public sealed class CreateTaskNoteController(CreateTaskNote handler) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<TaskNoteDto>> Create(int projectId, int id, SaveTaskNoteRequest request, CancellationToken ct)
    {
        var note = await handler.Execute(projectId, id, request, ct);
        return Created($"/api/projects/{projectId}/tasks/{id}/notes", note);
    }
}
