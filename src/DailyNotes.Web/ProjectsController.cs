using DailyNotes.Application;
using Microsoft.AspNetCore.Mvc;

namespace DailyNotes.Web;

[ApiController]
[Route("api/projects")]
public sealed class ProjectsController(NotesService service) : ControllerBase
{
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, [FromBody] DeleteProjectRequest request, CancellationToken ct)
    {
        await service.DeleteProject(id, request, ct);
        return NoContent();
    }

    [HttpGet("{id:int}/notes")]
    public Task<IReadOnlyList<ProjectNoteDto>> Notes(int id, CancellationToken ct) => service.GetProjectNotes(id, ct);

    [HttpPost("{id:int}/notes")]
    public async Task<ActionResult<ProjectNoteDto>> CreateNote(int id, SaveProjectNoteRequest request, CancellationToken ct)
    {
        var note = await service.CreateProjectNote(id, request, ct);
        return Created($"/api/projects/{id}/notes", note);
    }

    [HttpPut("{id:int}/notes/{noteId:int}")]
    public Task<ProjectNoteDto> UpdateNote(int id, int noteId, SaveProjectNoteRequest request, CancellationToken ct) =>
        service.UpdateProjectNote(id, noteId, request, ct);

    [HttpDelete("{id:int}/notes/{noteId:int}")]
    public async Task<IActionResult> DeleteNote(int id, int noteId, CancellationToken ct)
    {
        await service.DeleteProjectNote(id, noteId, ct);
        return NoContent();
    }

    [HttpGet("{id:int}")]
    public Task<ProjectDto> Get(int id, CancellationToken ct) => service.GetProject(id, ct);

    [HttpPut("{id:int}")]
    public Task<ProjectDto> Update(int id, UpdateProjectRequest request, CancellationToken ct) =>
        service.UpdateProject(id, request, ct);

    [HttpPut("{id:int}/status")]
    public Task<ProjectDto> SetStatus(int id, SetProjectStatusRequest request, CancellationToken ct) =>
        service.SetProjectStatus(id, request, ct);
}
