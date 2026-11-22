using JChrono.Application.Features.Notes;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Notes;

[ApiController]
[Route("api/notes")]
public sealed class DeleteNoteController(DeleteNote handler) : ControllerBase
{
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await handler.Execute(id, ct);
        return NoContent();
    }
}
