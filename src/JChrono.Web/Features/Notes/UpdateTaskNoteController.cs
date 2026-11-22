using JChrono.Application;
using JChrono.Application.Features.Notes;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Notes;

[ApiController]
[Route("api/projects/{projectId:int}/tasks/{id:int}/notes")]
public sealed class UpdateTaskNoteController(UpdateTaskNote handler) : ControllerBase
{
    [HttpPut("{noteId:int}")]
    public Task<TaskNoteDto> Update(int projectId, int id, int noteId, SaveTaskNoteRequest request, CancellationToken ct) =>
        handler.Execute(projectId, id, noteId, request, ct);
}
