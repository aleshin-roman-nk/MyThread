using JChrono.Application;
using JChrono.Application.Features.Diary;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace JChrono.Web.Features.Diary;

[ApiController]
[Route("api/diary/notes")]
public sealed class ListDiaryNotesController(ListDiaryNotes handler) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<DiaryNoteDto>> Get(
        [FromQuery, BindRequired] DateOnly from, [FromQuery, BindRequired] DateOnly through, CancellationToken ct) =>
        handler.Execute(from, through, ct);
}
