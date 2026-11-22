using JChrono.Application.Features.Diary;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Diary;

[ApiController]
[Route("api/diary/notes")]
public sealed class DeleteDiaryNoteController(DeleteDiaryNote handler) : ControllerBase
{
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await handler.Execute(id, ct);
        return NoContent();
    }
}
