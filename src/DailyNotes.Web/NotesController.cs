using DailyNotes.Application;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DailyNotes.Web;

[ApiController]
[Route("api/notes")]
public sealed class NotesController(NotesService service) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<NoteDto>> Get([FromQuery, BindRequired] DateOnly date, CancellationToken ct) =>
        service.GetNotes(date, ct);

    [HttpPost]
    public async Task<ActionResult<NoteDto>> Create(SaveNoteRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateNote(request, ct));

    [HttpPut("{id:int}")]
    public Task<NoteDto> Update(int id, SaveNoteRequest request, CancellationToken ct) =>
        service.UpdateNote(id, request, ct);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.DeleteNote(id, ct);
        return NoContent();
    }
}

