using JChrono.Application.Features.Notes;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Notes;

[ApiController]
[Route("api/projects/{id:int}/notes")]
public sealed class DeleteProjectNoteController(DeleteProjectNote handler) : ControllerBase
{
    [HttpDelete("{noteId:int}")]
    public async Task<IActionResult> Delete(int id, int noteId, CancellationToken ct)
    {
        await handler.Execute(id, noteId, ct);
        return NoContent();
    }
}
