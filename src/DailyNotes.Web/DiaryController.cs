using DailyNotes.Application;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DailyNotes.Web;

[ApiController]
[Route("api/diary/notes")]
public sealed class DiaryController(DiaryService service) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<DiaryNoteDto>> Get(
        [FromQuery, BindRequired] DateOnly from, [FromQuery, BindRequired] DateOnly through, CancellationToken ct) =>
        service.GetNotes(from, through, ct);

    [HttpPost]
    public async Task<ActionResult<DiaryNoteDto>> Create(SaveDiaryNoteRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateNote(request, ct));

    [HttpPut("{id:int}")]
    public Task<DiaryNoteDto> Update(int id, SaveDiaryNoteRequest request, CancellationToken ct) =>
        service.UpdateNote(id, request, ct);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.DeleteNote(id, ct);
        return NoContent();
    }
}
