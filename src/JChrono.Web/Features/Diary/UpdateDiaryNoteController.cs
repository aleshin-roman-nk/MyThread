using JChrono.Application;
using JChrono.Application.Features.Diary;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Diary;

[ApiController]
[Route("api/diary/notes")]
public sealed class UpdateDiaryNoteController(UpdateDiaryNote handler) : ControllerBase
{
    [HttpPut("{id:int}")]
    public Task<DiaryNoteDto> Update(int id, SaveDiaryNoteRequest request, CancellationToken ct) =>
        handler.Execute(id, request, ct);
}
