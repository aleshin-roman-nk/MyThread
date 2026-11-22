using JChrono.Application;
using JChrono.Application.Features.Notes;
using Microsoft.AspNetCore.Mvc;

namespace JChrono.Web.Features.Notes;

[ApiController]
[Route("api/projects/{projectId:int}/tasks/{id:int}/notes")]
public sealed class GetTaskNotesController(GetTaskNotes handler) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<TaskNoteDto>> Get(int projectId, int id, CancellationToken ct) =>
        handler.Execute(projectId, id, ct);
}
