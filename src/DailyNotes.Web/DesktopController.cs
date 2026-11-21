using DailyNotes.Application;
using Microsoft.AspNetCore.Mvc;

namespace DailyNotes.Web;

[ApiController]
[Route("api/desktop/stickers")]
public sealed class DesktopController(DesktopService service) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<StickerDto>> Get(CancellationToken ct) => service.GetStickers(ct);

    [HttpPost("text")]
    public Task<StickerDto> CreateText(SaveTextStickerRequest request, CancellationToken ct) => service.CreateText(request, ct);

    [HttpPut("{id:int}/text")]
    public Task<StickerDto> UpdateText(int id, SaveTextStickerRequest request, CancellationToken ct) => service.UpdateText(id, request, ct);

    [HttpDelete("{id:int}/text")]
    public async Task<IActionResult> DeleteText(int id, CancellationToken ct)
    {
        await service.DeleteText(id, ct);
        return NoContent();
    }

    [HttpGet("tasks/{taskId:int}")]
    public Task<TaskPinStatusDto> GetTaskPinStatus(int taskId, CancellationToken ct) => service.GetTaskPinStatus(taskId, ct);

    [HttpPut("tasks/{taskId:int}")]
    public Task<StickerDto> PinTask(int taskId, CancellationToken ct) => service.PinTask(taskId, ct);

    [HttpDelete("tasks/{taskId:int}")]
    public async Task<IActionResult> UnpinTask(int taskId, CancellationToken ct)
    {
        await service.UnpinTask(taskId, ct);
        return NoContent();
    }

    [HttpPut("{id:int}/position")]
    public Task<StickerDto> Move(int id, MoveStickerRequest request, CancellationToken ct) => service.Move(id, request, ct);

    [HttpGet("notes/{noteId:int}")]
    public Task<TaskPinStatusDto> GetNotePinStatus(int noteId, CancellationToken ct) => service.GetNotePinStatus(noteId, ct);

    [HttpPut("notes/{noteId:int}")]
    public Task<StickerDto> PinNote(int noteId, CancellationToken ct) => service.PinNote(noteId, ct);

    [HttpDelete("notes/{noteId:int}")]
    public async Task<IActionResult> UnpinNote(int noteId, CancellationToken ct)
    {
        await service.UnpinNote(noteId, ct);
        return NoContent();
    }
}
