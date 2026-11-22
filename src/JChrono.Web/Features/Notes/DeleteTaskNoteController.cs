using JChrono.Application.Features.Notes;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Notes;

[ApiController]
[Route("api/projects/{projectId:int}/tasks/{id:int}/notes")]
public sealed class DeleteTaskNoteController(DeleteTaskNote handler) : ControllerBase
{
    [HttpDelete("{noteId:int}")]
    public async Task<IActionResult> Delete(int projectId, int id, int noteId, CancellationToken ct)
    {
        await handler.Execute(projectId, id, noteId, ct);
        return NoContent();
    }
}
