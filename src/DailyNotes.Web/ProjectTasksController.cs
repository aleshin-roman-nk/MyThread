using DailyNotes.Application;
using DailyNotes.Domain;
using Microsoft.AspNetCore.Mvc;

namespace DailyNotes.Web;

[ApiController]
[Route("api/projects/{projectId:int}/tasks")]
public sealed class ProjectTasksController(NotesService service) : ControllerBase
{
    [HttpGet("{id:int}/notes")]
    public Task<IReadOnlyList<TaskNoteDto>> Notes(int projectId, int id, CancellationToken ct) => service.GetTaskNotes(projectId, id, ct);

    [HttpPost("{id:int}/notes")]
    public async Task<ActionResult<TaskNoteDto>> CreateNote(int projectId, int id, SaveTaskNoteRequest request, CancellationToken ct)
    {
        var note = await service.CreateTaskNote(projectId, id, request, ct);
        return Created($"/api/projects/{projectId}/tasks/{id}/notes", note);
    }

    [HttpPut("{id:int}/notes/{noteId:int}")]
    public Task<TaskNoteDto> UpdateNote(int projectId, int id, int noteId, SaveTaskNoteRequest request, CancellationToken ct) =>
        service.UpdateTaskNote(projectId, id, noteId, request, ct);

    [HttpDelete("{id:int}/notes/{noteId:int}")]
    public async Task<IActionResult> DeleteNote(int projectId, int id, int noteId, CancellationToken ct)
    {
        await service.DeleteTaskNote(projectId, id, noteId, ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int projectId, int id, [FromBody] DeleteTaskRequest request, CancellationToken ct)
    {
        await service.DeleteProjectTask(projectId, id, request, ct);
        return NoContent();
    }

    [HttpGet]
    public Task<IReadOnlyList<ProjectTaskDto>> Get(int projectId, ProjectTaskStatus? status, CancellationToken ct) => service.GetProjectTasks(projectId, ct, status);

    [HttpPut("{id:int}")]
    public Task<ProjectTaskDto> Update(int projectId, int id, UpdateProjectTaskRequest request, CancellationToken ct) =>
        service.UpdateProjectTask(projectId, id, request, ct);

    [HttpPost]
    public async Task<ActionResult<ProjectTaskDto>> Create(int projectId, CreateProjectTaskRequest request, CancellationToken ct)
    {
        var task = await service.CreateProjectTask(projectId, request, ct);
        return CreatedAtAction(nameof(Get), new { projectId }, task);
    }
}
