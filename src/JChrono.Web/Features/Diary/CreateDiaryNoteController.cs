using JChrono.Application;
using JChrono.Application.Features.Diary;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Diary;

[ApiController]
[Route("api/diary/notes")]
public sealed class CreateDiaryNoteController(CreateDiaryNote handler) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<DiaryNoteDto>> Create(SaveDiaryNoteRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await handler.Execute(request, ct));
}
